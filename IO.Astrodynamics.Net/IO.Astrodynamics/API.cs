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
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;
using Instrument = IO.Astrodynamics.Body.Spacecraft.Instrument;
using Launch = IO.Astrodynamics.DTO.Launch;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.DTO.StateOrientation;
using StateVector = IO.Astrodynamics.DTO.StateVector;
using Window = IO.Astrodynamics.DTO.Window;

namespace IO.Astrodynamics;

/// <summary>
///     API to communicate with IO.Astrodynamics
/// </summary>
public class API
{
    private readonly List<FileSystemInfo> _kernels = [];

    /// <summary>
    ///     Instantiate API
    /// </summary>
    private API()
    {
        NativeLibrary.SetDllImportResolver(typeof(API).Assembly, Resolver);
    }

    public static API Instance { get; } = new();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetSpiceVersionProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void LaunchProxy([In] [Out] ref Launch launch);

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

    private static IntPtr Resolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var libHandle = IntPtr.Zero;

        if (libraryName != "IO.Astrodynamics") return libHandle;
        string sharedLibName = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            sharedLibName = "resources/IO.Astrodynamics.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) sharedLibName = "resources/libIO.Astrodynamics.so";

        if (!string.IsNullOrEmpty(sharedLibName))
            NativeLibrary.TryLoad(sharedLibName, typeof(API).Assembly, DllImportSearchPath.AssemblyDirectory,
                out libHandle);
        else
            throw new PlatformNotSupportedException();

        return libHandle;
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
        }
    }

    /// <summary>
    ///     Find launch windows
    /// </summary>
    /// <param name="launch"></param>
    /// <param name="window"></param>
    /// <param name="outputDirectory"></param>
    public IEnumerable<LaunchWindow> FindLaunchWindows(Maneuver.Launch launch,
        in Time.Window window, DirectoryInfo outputDirectory)
    {
        if (launch == null) throw new ArgumentNullException(nameof(launch));
        lock (lockObject)
        {
            //Convert data
            Launch launchDto = launch.Convert();
            launchDto.Window = window.Convert();
            launchDto.LaunchSite.DirectoryPath = outputDirectory.CreateSubdirectory("Sites").FullName;
            launchDto.RecoverySite.DirectoryPath = outputDirectory.CreateSubdirectory("Sites").FullName;

            //Execute request
            LaunchProxy(ref launchDto);

            //Filter result
            var windows = launchDto.Windows.Where(x => x.Start != 0 && x.End != 0).ToArray();

            //Build result 
            List<LaunchWindow> launchWindows = [];

            for (int i = 0; i < windows.Length; i++)
            {
                launchWindows.Add(new LaunchWindow(windows[i].Convert(),
                    launchDto.InertialInsertionVelocity[i], launchDto.NonInertialInsertionVelocity[i],
                    launchDto.InertialAzimuth[i], launchDto.NonInertialAzimuth[i]));
            }

            return launchWindows;
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
    public IEnumerable<Time.Window> FindWindowsOnDistanceConstraint(Time.Window searchWindow, INaifObject observer,
        INaifObject target, RelationnalOperator relationalOperator, double value, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        lock (lockObject)
        {
            return FindWindowsOnDistanceConstraint(searchWindow, observer.NaifId, target.NaifId, relationalOperator, value, aberration, stepSize);
        }
    }

    public IEnumerable<Time.Window> FindWindowsOnDistanceConstraint(Time.Window searchWindow, int observerId,
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
    public IEnumerable<Time.Window> FindWindowsOnOccultationConstraint(Time.Window searchWindow, INaifObject observer,
        INaifObject target, ShapeType targetShape, INaifObject frontBody, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (frontBody == null) throw new ArgumentNullException(nameof(frontBody));
        lock (lockObject)
        {
            string frontFrame = frontShape == ShapeType.Ellipsoid
                ? (frontBody as Body.CelestialBody)?.Frame.Name
                : string.Empty;
            string targetFrame = targetShape == ShapeType.Ellipsoid
                ? (target as Body.CelestialBody)?.Frame.Name
                : String.Empty;
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnOccultationConstraintProxy(searchWindow.Convert(), observer.NaifId, target.NaifId,
                targetFrame, targetShape.GetDescription(),
                frontBody.NaifId, frontFrame, frontShape.GetDescription(), occultationType.GetDescription(),
                aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
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
    public IEnumerable<Time.Window> FindWindowsOnOccultationConstraint(Time.Window searchWindow, int observerId,
        int targetId, ShapeType targetShape, int frontBodyId, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize)
    {
        lock (lockObject)
        {
            IO.Astrodynamics.Body.CelestialBody frontBody = new IO.Astrodynamics.Body.CelestialBody(frontBodyId);
            IO.Astrodynamics.Body.CelestialBody targetBody = new IO.Astrodynamics.Body.CelestialBody(targetId);
            string frontFrame = frontShape == ShapeType.Ellipsoid
                ? frontBody.Frame.Name
                : string.Empty;
            string targetFrame = targetShape == ShapeType.Ellipsoid
                ? targetBody.Frame.Name
                : String.Empty;
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnOccultationConstraintProxy(searchWindow.Convert(), observerId, targetId, targetFrame, targetShape.GetDescription(),
                frontBody.NaifId, frontFrame, frontShape.GetDescription(), occultationType.GetDescription(), aberration.GetDescription(), stepSize.TotalSeconds, windows);
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
    public IEnumerable<Time.Window> FindWindowsOnCoordinateConstraint(Time.Window searchWindow, INaifObject observer,
        INaifObject target, Frame frame, CoordinateSystem coordinateSystem, Coordinate coordinate,
        RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnCoordinateConstraintProxy(searchWindow.Convert(), observer.NaifId, target.NaifId,
                frame.Name, coordinateSystem.GetDescription(),
                coordinate.GetDescription(), relationalOperator.GetDescription(), value, adjustValue,
                aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
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
    public IEnumerable<Time.Window> FindWindowsOnCoordinateConstraint(Time.Window searchWindow, int observerId,
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
    public IEnumerable<Time.Window> FindWindowsOnIlluminationConstraint(Time.Window searchWindow, INaifObject observer,
        INaifObject targetBody, Frame fixedFrame,
        Coordinates.Planetodetic planetodetic, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
        double value, double adjustValue, Aberration aberration, TimeSpan stepSize, INaifObject illuminationSource,
        string method = "Ellipsoid")
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (targetBody == null) throw new ArgumentNullException(nameof(targetBody));
        if (fixedFrame == null) throw new ArgumentNullException(nameof(fixedFrame));
        if (illuminationSource == null) throw new ArgumentNullException(nameof(illuminationSource));
        if (method == null) throw new ArgumentNullException(nameof(method));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnIlluminationConstraintProxy(searchWindow.Convert(), observer.NaifId,
                illuminationSource.Name, targetBody.NaifId, fixedFrame.Name,
                planetodetic.Convert(),
                illuminationType.GetDescription(), relationalOperator.GetDescription(), value, adjustValue,
                aberration.GetDescription(), stepSize.TotalSeconds, method, windows);
            return windows.Where(x => !double.IsNaN(x.Start)).Select(x => x.Convert());
        }
    }

    public IEnumerable<Time.Window> FindWindowsOnIlluminationConstraint(Time.Window searchWindow, int observerId,
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
    public IEnumerable<Time.Window> FindWindowsInFieldOfViewConstraint(Time.Window searchWindow, Spacecraft observer,
        Instrument instrument, INaifObject target, Frame targetFrame, ShapeType targetShape, Aberration aberration,
        TimeSpan stepSize)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (instrument == null) throw new ArgumentNullException(nameof(instrument));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
        lock (lockObject)
        {
            return FindWindowsInFieldOfViewConstraint(searchWindow, observer.NaifId, instrument.NaifId, target.NaifId, targetFrame, targetShape, aberration, stepSize);
        }
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
    public IEnumerable<Time.Window> FindWindowsInFieldOfViewConstraint(Time.Window searchWindow, int observerId,
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
    public IEnumerable<OrbitalParameters.OrbitalParameters> ReadEphemeris(Time.Window searchWindow,
        ILocalizable observer, ILocalizable target, Frame frame,
        Aberration aberration, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            const int messageSize = 10000;
            List<OrbitalParameters.OrbitalParameters> orbitalParameters = [];
            int occurrences = (int)(searchWindow.Length / stepSize / messageSize);

            for (int i = 0; i <= occurrences; i++)
            {
                var start = searchWindow.StartDate + i * messageSize * stepSize;
                var end = start + messageSize * stepSize > searchWindow.EndDate ? searchWindow.EndDate : (start + messageSize * stepSize) - stepSize;
                var window = new Time.Window(start, end);
                var stateVectors = new StateVector[messageSize];
                ReadEphemerisProxy(window.Convert(), observer.NaifId, target.NaifId, frame.Name,
                    aberration.GetDescription(), stepSize.TotalSeconds,
                    stateVectors);
                orbitalParameters.AddRange(stateVectors.Where(x => !string.IsNullOrEmpty(x.Frame)).Select(x =>
                    new OrbitalParameters.StateVector(x.Position.Convert(), x.Velocity.Convert(), observer, DateTimeExtension.CreateTDB(x.Epoch), frame)));
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
    public OrbitalParameters.OrbitalParameters ReadEphemeris(DateTime epoch, ILocalizable observer,
        ILocalizable target, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var stateVector = ReadEphemerisAtGivenEpochProxy(epoch.SecondsFromJ2000TDB(), observer.NaifId,
                target.NaifId, frame.Name, aberration.GetDescription());
            return new OrbitalParameters.StateVector(stateVector.Position.Convert(), stateVector.Velocity.Convert(), observer,
                DateTimeExtension.CreateTDB(stateVector.Epoch), frame);
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
    public IEnumerable<OrbitalParameters.StateOrientation> ReadOrientation(Time.Window searchWindow,
        Spacecraft spacecraft, TimeSpan tolerance,
        Frame referenceFrame, TimeSpan stepSize)
    {
        if (spacecraft == null) throw new ArgumentNullException(nameof(spacecraft));
        if (referenceFrame == null) throw new ArgumentNullException(nameof(referenceFrame));
        lock (lockObject)
        {
            var stateOrientations = new StateOrientation[10000];
            ReadOrientationProxy(searchWindow.Convert(), spacecraft.NaifId, tolerance.TotalSeconds,
                referenceFrame.Name, stepSize.TotalSeconds,
                stateOrientations);
            return stateOrientations.Where(x => x.Frame != null).Select(x => new OrbitalParameters.StateOrientation(
                x.Rotation.Convert(), x.AngularVelocity.Convert(), DateTimeExtension.CreateTDB(x.Epoch), referenceFrame));
        }
    }

    /// <summary>
    ///     Write ephemeris file
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="naifObject"></param>
    /// <param name="stateVectors"></param>
    /// <returns></returns>
    public bool WriteEphemeris(FileInfo filePath, INaifObject naifObject,
        IEnumerable<OrbitalParameters.StateVector> stateVectors)
    {
        if (naifObject == null) throw new ArgumentNullException(nameof(naifObject));
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateVectors == null) throw new ArgumentNullException(nameof(stateVectors));
        lock (lockObject)
        {
            var enumerable = stateVectors as OrbitalParameters.StateVector[] ?? stateVectors.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateVectors));
            bool res = WriteEphemerisProxy(filePath.FullName, naifObject.NaifId, stateVectors.Select(x => x.Convert()).ToArray(),
                (uint)enumerable.Length);
            if (res == false)
            {
                throw new InvalidOperationException(
                    "An error occurred while writing ephemeris. You can have more details on standard output.");
            }

            return true;
        }
    }

    public bool WriteOrientation(FileInfo filePath, INaifObject naifObject, IEnumerable<OrbitalParameters.StateOrientation> stateOrientations)
    {
        if (naifObject == null) throw new ArgumentNullException(nameof(naifObject));
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateOrientations == null) throw new ArgumentNullException(nameof(stateOrientations));
        lock (lockObject)
        {
            var enumerable = stateOrientations as OrbitalParameters.StateOrientation[] ?? stateOrientations.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateOrientations));
            bool res = WriteOrientationProxy(filePath.FullName, naifObject.NaifId, stateOrientations.Select(x => x.Convert()).ToArray(), (uint)enumerable.Length);
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
    public OrbitalParameters.StateOrientation TransformFrame(Frame fromFrame, Frame toFrame, DateTime epoch)
    {
        lock (lockObject)
        {
            if (fromFrame == null) throw new ArgumentNullException(nameof(fromFrame));
            if (toFrame == null) throw new ArgumentNullException(nameof(toFrame));
            var res = TransformFrameProxy(fromFrame.Name, toFrame.Name, epoch.ToTDB().SecondsFromJ2000TDB());

            return new OrbitalParameters.StateOrientation(
                new Quaternion(res.Rotation.W, res.Rotation.X, res.Rotation.Y, res.Rotation.Z),
                new Vector3(res.AngularVelocity.X, res.AngularVelocity.Y, res.AngularVelocity.Z), epoch, fromFrame);
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
    public OrbitalParameters.StateVector ConvertTleToStateVector(string line1, string line2, string line3,
        DateTime epoch)
    {
        lock (lockObject)
        {
            var res = ConvertTLEToStateVectorProxy(line1, line2, line3, epoch.SecondsFromJ2000TDB());
            return new OrbitalParameters.StateVector(res.Position.Convert(), res.Velocity.Convert(),
                new Body.CelestialBody(PlanetsAndMoons.EARTH, Frame.ECLIPTIC_J2000, epoch), epoch,
                new Frame(res.Frame));
        }
    }

    /// <summary>
    /// Create TLE
    /// </summary>
    /// <param name="line1">CelestialItem identifier</param>
    /// <param name="line2">line 1</param>
    /// <param name="line3">line 2</param>
    /// <returns></returns>
    public TLE CreateTLE(string line1, string line2, string line3)
    {
        lock (lockObject)
        {
            var res = GetTLEElementsProxy(line1, line2, line3);

            return new TLE(line1, line2, line3, res.BalisticCoefficient, res.DragTerm, res.SecondDerivativeOfMeanMotion,
                res.A, res.E, res.I, res.O, res.W, res.M,
                DateTimeExtension.CreateTDB(res.Epoch), Frame.ICRF);
        }
    }
}