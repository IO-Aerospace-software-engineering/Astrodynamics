using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// Comprehensive smoke tests exercising every public SpiceAPI method.
/// These tests verify that the native P/Invoke layer is correctly loaded
/// and all entry points are reachable.
/// </summary>
public class SpiceAPISmokeTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public SpiceAPISmokeTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static CelestialBody EarthAtJ2000 =>
        new(PlanetsAndMoons.EARTH, Frame.ICRF, new Time(2000, 1, 1, 12, 0, 0));

    private static CelestialBody MoonAtJ2000 =>
        new(PlanetsAndMoons.MOON, Frame.ICRF, new Time(2000, 1, 1, 12, 0, 0));

    private static CelestialBody Sun => new(Stars.Sun);

    // ===== Kernel Management =====

    [Fact]
    public void GetSpiceVersion_ReturnsNonEmpty()
    {
        var version = SpiceAPI.Instance.GetSpiceVersion();
        Assert.False(string.IsNullOrEmpty(version));
    }

    [Fact]
    public void LoadKernels_ReturnsLoadedKernels()
    {
        var kernels = SpiceAPI.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void LoadAndUnloadKernels()
    {
        var tempDir = new DirectoryInfo(SolarSystemKernelPath.FullName);
        // Kernels are already loaded in constructor; just verify unload/reload cycle works
        SpiceAPI.Instance.UnloadKernels(tempDir);
        SpiceAPI.Instance.LoadKernels(tempDir);
        var kernels = SpiceAPI.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void ClearKernels_ThenReload()
    {
        SpiceAPI.Instance.ClearKernels();
        // Reload for subsequent tests
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
        var kernels = SpiceAPI.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    // ===== GetCelestialBodyInfo =====

    [Fact]
    public void GetCelestialBodyInfo_Earth()
    {
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(PlanetsAndMoons.EARTH.NaifId);
        Assert.Equal(399, info.Id);
        Assert.Equal("EARTH", info.Name);
        Assert.True(info.GM > 0);
        Assert.True(info.Radii.X > 0);
    }

    [Fact]
    public void GetCelestialBodyInfo_Moon()
    {
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(PlanetsAndMoons.MOON.NaifId);
        Assert.Equal(301, info.Id);
        Assert.Equal("MOON", info.Name);
        Assert.True(info.GM > 0);
    }

    // ===== ReadEphemeris =====

    [Fact]
    public void ReadEphemeris_Windowed()
    {
        var searchWindow = new Window(Time.CreateTDB(0.0), Time.CreateTDB(100.0));
        var results = SpiceAPI.Instance.ReadEphemeris(searchWindow, PlanetsAndMoons.EARTH.NaifId,
                PlanetsAndMoons.MOON.NaifId, Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0))
            .Select(x => x.ToStateVector()).ToArray();

        Assert.NotEmpty(results);
        Assert.NotEqual(0.0, results[0].Position.Magnitude());
        Assert.NotEqual(0.0, results[0].Velocity.Magnitude());
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, results[0].Observer.NaifId);
        Assert.Equal(Frame.ICRF, results[0].Frame);
    }

    [Fact]
    public void ReadEphemeris_SingleEpoch()
    {
        var epoch = Time.CreateTDB(0.0);
        var result = SpiceAPI.Instance.ReadEphemeris(epoch, PlanetsAndMoons.EARTH.NaifId,
            PlanetsAndMoons.MOON.NaifId, Frame.ICRF, Aberration.LT);

        var sv = result.ToStateVector();
        Assert.NotEqual(0.0, sv.Position.Magnitude());
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, sv.Observer.NaifId);
    }

    [Fact]
    public void ReadEphemeris_WithObsoleteOverload()
    {
#pragma warning disable CS0618 // Obsolete
        var searchWindow = new Window(Time.CreateTDB(0.0), Time.CreateTDB(100.0));
        var results = SpiceAPI.Instance.ReadEphemeris(searchWindow, EarthAtJ2000, MoonAtJ2000,
                Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(50.0))
            .Select(x => x.ToStateVector()).ToArray();
        Assert.NotEmpty(results);

        var epoch = Time.CreateTDB(0.0);
        var result = SpiceAPI.Instance.ReadEphemeris(epoch, EarthAtJ2000, MoonAtJ2000,
            Frame.ICRF, Aberration.None);
        Assert.NotEqual(0.0, result.ToStateVector().Position.Magnitude());
#pragma warning restore CS0618
    }

    [Fact]
    public void ReadEphemeris_SsbObserver()
    {
        var epoch = Time.CreateTDB(0.0);
        var result = SpiceAPI.Instance.ReadEphemeris(epoch, 0, PlanetsAndMoons.EARTH.NaifId,
            Frame.ICRF, Aberration.None);
        var sv = result.ToStateVector();
        Assert.Equal(0, sv.Observer.NaifId);
        Assert.NotEqual(0.0, sv.Position.Magnitude());
    }

    // ===== WriteEphemeris & Read back =====

    [Fact]
    public void WriteEphemeris_WithNaifId()
    {
        const int size = 10;
        var sv = new StateVector[size];
        for (int i = 0; i < size; ++i)
        {
            sv[i] = new StateVector(new Vector3(6800000 + i * 100, i * 10, i * 10),
                new Vector3(i, 7656.0 + i * 0.001, i),
                EarthAtJ2000, Time.CreateTDB(i), Frame.ICRF);
        }

        var file = new FileInfo("SmokeTest_Ephemeris.spk");
        try
        {
            SpiceAPI.Instance.WriteEphemeris(file, -900, sv);
            SpiceAPI.Instance.LoadKernels(file);

            var window = new Window(Time.J2000TDB, Time.J2000TDB.AddSeconds(9.0));
            var results = SpiceAPI.Instance.ReadEphemeris(window, PlanetsAndMoons.EARTH.NaifId, -900,
                    Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(1.0))
                .Select(x => x.ToStateVector()).ToArray();
            Assert.Equal(size, results.Length);
            Assert.Equal(6800000.0, results[0].Position.X, 3);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
            if (file.Exists) file.Delete();
        }
    }

    [Fact]
    public void WriteEphemeris_WithObsoleteOverload()
    {
#pragma warning disable CS0618
        const int size = 10;
        Clock clock = new Clock("smokeClk1", 65536);
        var spacecraft = new Spacecraft(-901, "SmokeSpc1", 1000.0, 5000.0, clock,
            new StateVector(new Vector3(6800000, 0, 0), new Vector3(0, 7656.0, 0),
                EarthAtJ2000, Time.CreateTDB(0.0), Frame.ICRF));

        var sv = new StateVector[size];
        for (int i = 0; i < size; ++i)
        {
            sv[i] = new StateVector(new Vector3(6800000 + i * 100, i * 10, i * 10),
                new Vector3(i, 7656.0 + i * 0.001, i),
                EarthAtJ2000, Time.CreateTDB(i), Frame.ICRF);
        }

        var file = new FileInfo("SmokeTest_Ephemeris_Obsolete.spk");
        try
        {
            SpiceAPI.Instance.WriteEphemeris(file, spacecraft, sv);
            Assert.True(file.Exists);
        }
        finally
        {
            if (file.Exists) file.Delete();
        }
#pragma warning restore CS0618
    }

    // ===== WriteOrientation & ReadOrientation =====

    [Fact]
    public async Task WriteAndReadOrientation()
    {
        const int size = 10;
        Clock clock = new Clock("smokeClk2", 65536);
        var spacecraft = new Spacecraft(-902, "SmokeSpc2", 1000.0, 5000.0, clock,
            new StateVector(new Vector3(6800000, 0, 0), new Vector3(0, 7656.0, 0),
                EarthAtJ2000, Time.CreateTDB(0.0), Frame.ICRF));

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(
                new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3),
                Vector3.Zero, Time.CreateTDB(i), Frame.ICRF);
        }

        var clockFile = new FileInfo("SmokeTest_Clock.tsc");
        var ckFile = new FileInfo("SmokeTest_Orientation.ck");
        try
        {
            await clock.WriteAsync(clockFile);
            SpiceAPI.Instance.LoadKernels(clockFile);

            SpiceAPI.Instance.WriteOrientation(ckFile, spacecraft.NaifId, so);
            SpiceAPI.Instance.LoadKernels(ckFile);

            var window = new Window(Time.J2000TDB, Time.J2000TDB.AddSeconds(9.0));
            var results = SpiceAPI.Instance.ReadOrientation(window, spacecraft.NaifId,
                TimeSpan.Zero, Frame.ICRF, TimeSpan.FromSeconds(1.0)).ToArray();

            Assert.NotEmpty(results);
            Assert.Equal(Frame.ICRF, results[0].ReferenceFrame);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(clockFile);
            if (ckFile.Exists) ckFile.Delete();
            if (clockFile.Exists) clockFile.Delete();
        }
    }

    [Fact]
    public async Task WriteAndReadOrientation_ObsoleteOverloads()
    {
#pragma warning disable CS0618
        const int size = 10;
        Clock clock = new Clock("smokeClk3", 65536);
        var spacecraft = new Spacecraft(-903, "SmokeSpc3", 1000.0, 5000.0, clock,
            new StateVector(new Vector3(6800000, 0, 0), new Vector3(0, 7656.0, 0),
                EarthAtJ2000, Time.CreateTDB(0.0), Frame.ICRF));

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(
                new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3),
                Vector3.Zero, Time.CreateTDB(i), Frame.ICRF);
        }

        var clockFile = new FileInfo("SmokeTest_Clock_Obsolete.tsc");
        var ckFile = new FileInfo("SmokeTest_Orientation_Obsolete.ck");
        try
        {
            await clock.WriteAsync(clockFile);
            SpiceAPI.Instance.LoadKernels(clockFile);

            SpiceAPI.Instance.WriteOrientation(ckFile, spacecraft, so);
            SpiceAPI.Instance.LoadKernels(ckFile);

            var window = new Window(Time.J2000TDB, Time.J2000TDB.AddSeconds(9.0));
            var results = SpiceAPI.Instance.ReadOrientation(window, spacecraft,
                TimeSpan.Zero, Frame.ICRF, TimeSpan.FromSeconds(1.0)).ToArray();

            Assert.NotEmpty(results);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(clockFile);
            if (ckFile.Exists) ckFile.Delete();
            if (clockFile.Exists) clockFile.Delete();
        }
#pragma warning restore CS0618
    }

    // ===== TransformFrame =====

    [Fact]
    public void TransformFrame_SingleEpoch()
    {
        var result = SpiceAPI.Instance.TransformFrame(Time.J2000TDB, Frame.ICRF,
            new Frame(PlanetsAndMoons.EARTH.Frame));

        Assert.NotEqual(0.0, result.Rotation.W);
        Assert.NotEqual(0.0, result.AngularVelocity.Magnitude());
    }

    [Fact]
    public void TransformFrame_Windowed()
    {
        var window = new Window(Time.J2000TDB, Time.J2000TDB.AddDays(1.0));
        var results = SpiceAPI.Instance.TransformFrame(window, Frame.ICRF,
            new Frame(PlanetsAndMoons.EARTH.Frame), TimeSpan.FromDays(0.5)).ToArray();

        Assert.True(results.Length >= 2);
        Assert.NotEqual(results[0].Rotation.W, results[1].Rotation.W);
    }

    // ===== ConvertTleToStateVector =====

    [Fact]
    public void ConvertTleToStateVector()
    {
        var result = SpiceAPI.Instance.ConvertTleToStateVector(
            "ISS",
            "1 25544U 98067A   24001.00000000  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703",
            new Time(2024, 1, 1, frame: TimeFrame.UTCFrame));

        var sv = result.ToStateVector();
        Assert.NotEqual(0.0, sv.Position.Magnitude());
        Assert.NotEqual(0.0, sv.Velocity.Magnitude());
    }

    // ===== ConvertStateVectorToConicOrbitalElement =====

    [Fact]
    public void ConvertStateVectorToConicOrbitalElement_DefaultGM()
    {
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);

        var ke = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        Assert.True(ke.A > 0);
        Assert.True(ke.E >= 0 && ke.E < 1);
    }

    [Fact]
    public void ConvertStateVectorToConicOrbitalElement_ExplicitGM()
    {
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);

        var gm = EarthAtJ2000.GM;
        var ke = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state, gm);
        Assert.True(ke.A > 0);
    }

    // ===== ConvertConicElementsToStateVector =====

    [Fact]
    public void ConvertConicElementsToStateVector_AtEpoch()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, Time.J2000TDB);
        Assert.NotEqual(0.0, state.Position.Magnitude());
        Assert.NotEqual(0.0, state.Velocity.Magnitude());
    }

    [Fact]
    public void ConvertConicElementsToStateVector_Direct()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke);
        Assert.NotEqual(0.0, state.Position.Magnitude());
    }

    [Fact]
    public void ConvertConicElementsToStateVector_WithExplicitGM()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var gm = EarthAtJ2000.GM;
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, Time.J2000TDB, gm);
        Assert.NotEqual(0.0, state.Position.Magnitude());
    }

    [Fact]
    public void ConvertConicElements_RoundTrip()
    {
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, Time.J2000TDB);
        var ke2 = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        Assert.Equal(ke.A, ke2.A, 6);
        Assert.Equal(ke.E, ke2.E, 6);
        Assert.Equal(ke.I, ke2.I, 6);
        Assert.Equal(ke.RAAN, ke2.RAAN, 6);
        Assert.Equal(ke.AOP, ke2.AOP, 6);
        Assert.Equal(ke.M, ke2.M, 6);
    }

    [Fact]
    public void ConvertConicElements_Hyperbolic()
    {
        var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var state = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke, Time.J2000TDB);
        var ke2 = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        Assert.Equal(ke.A, ke2.A, 6);
        Assert.Equal(ke.E, ke2.E, 6);
    }

    // ===== ConvertEquinoctialElementsToStateVector =====

    [Fact]
    public void ConvertEquinoctialElementsToStateVector()
    {
        // Create Keplerian, convert to equinoctial, then convert to state vector via SpiceAPI
        var ke = new KeplerianElements(6800000.0, 0.1, 0.2, 0.3, 0.4, 0.5,
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var ee = ke.ToEquinoctial();
        var state = SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(ee);
        Assert.NotEqual(0.0, state.Position.Magnitude());
        Assert.NotEqual(0.0, state.Velocity.Magnitude());
    }

    // ===== Propagate2Bodies =====

    [Fact]
    public void Propagate2Bodies_DefaultGM()
    {
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var result = SpiceAPI.Instance.Propagate2Bodies(sv, TimeSpan.FromSeconds(60.0));
        Assert.NotEqual(0.0, result.Position.Magnitude());
        Assert.NotEqual(sv.Position.X, result.Position.X);
    }

    [Fact]
    public void Propagate2Bodies_ExplicitGM()
    {
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var gm = EarthAtJ2000.GM;
        var result = SpiceAPI.Instance.Propagate2Bodies(sv, gm, TimeSpan.FromSeconds(60.0));
        Assert.NotEqual(0.0, result.Position.Magnitude());
        Assert.NotEqual(sv.Position.X, result.Position.X);
    }

    [Fact]
    public void Propagate2Bodies_ToEpoch()
    {
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.J2000TDB, Frame.ICRF);
        var targetEpoch = Time.J2000TDB.AddSeconds(3600.0);
        var result = SpiceAPI.Instance.Propagate2Bodies(sv, targetEpoch);
        Assert.NotEqual(0.0, result.Position.Magnitude());
    }

    // ===== FindWindowsOnDistanceConstraint =====

    [Fact]
    public void FindWindowsOnDistanceConstraint()
    {
        var searchWindow = new Window(Time.CreateTDB(220881665.18391809),
            Time.CreateTDB(228657665.18565452));
        var results = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
            searchWindow, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId,
            RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0)).ToArray();
        Assert.NotEmpty(results);
    }

    [Fact]
    public void FindWindowsOnDistanceConstraint_ObsoleteOverload()
    {
#pragma warning disable CS0618
        var searchWindow = new Window(Time.CreateTDB(220881665.18391809),
            Time.CreateTDB(228657665.18565452));
        var results = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
            searchWindow, EarthAtJ2000, MoonAtJ2000,
            RelationnalOperator.Greater, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0)).ToArray();
        Assert.NotEmpty(results);
#pragma warning restore CS0618
    }

    // ===== FindWindowsOnOccultationConstraint =====

    [Fact]
    public void FindWindowsOnOccultationConstraint()
    {
        var searchWindow = new Window(Time.CreateTDB(61473664.183390938),
            Time.CreateTDB(61646464.183445148));
        var results = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(
            searchWindow, PlanetsAndMoons.EARTH.NaifId, Stars.Sun.NaifId,
            ShapeType.Ellipsoid, PlanetsAndMoons.MOON.NaifId, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)).ToArray();
        Assert.NotEmpty(results);
    }

    [Fact]
    public void FindWindowsOnOccultationConstraint_ObsoleteOverload()
    {
#pragma warning disable CS0618
        var searchWindow = new Window(Time.CreateTDB(61473664.183390938),
            Time.CreateTDB(61646464.183445148));
        var results = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(
            searchWindow, EarthAtJ2000, Sun, ShapeType.Ellipsoid,
            MoonAtJ2000, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)).ToArray();
        Assert.NotEmpty(results);
#pragma warning restore CS0618
    }

    // ===== FindWindowsOnCoordinateConstraint =====

    [Fact]
    public void FindWindowsOnCoordinateConstraint()
    {
        var site = new Site(13, "DSS-13", EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad,
                35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));

        var searchWindow = new Window(Time.CreateTDB(730036800.0),
            Time.CreateTDB(730123200));
        var results = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(
            searchWindow, site.NaifId, PlanetsAndMoons.MOON.NaifId,
            site.Frame, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
            TimeSpan.FromSeconds(60.0)).ToArray();
        Assert.NotEmpty(results);
    }

    [Fact]
    public void FindWindowsOnCoordinateConstraint_ObsoleteOverload()
    {
#pragma warning disable CS0618
        var site = new Site(13, "DSS-13", EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad,
                35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));

        var searchWindow = new Window(Time.CreateTDB(730036800.0),
            Time.CreateTDB(730123200));
        var results = SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(
            searchWindow, site, MoonAtJ2000,
            site.Frame, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 0.0, 0.0, Aberration.None,
            TimeSpan.FromSeconds(60.0)).ToArray();
        Assert.NotEmpty(results);
#pragma warning restore CS0618
    }

    // ===== FindWindowsOnIlluminationConstraint =====

    [Fact]
    public void FindWindowsOnIlluminationConstraint()
    {
        var searchWindow = new Window(Time.CreateTDB(674524800),
            Time.CreateTDB(674611200));
        var results = SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(
            searchWindow, Stars.Sun.NaifId, PlanetsAndMoons.EARTH.NaifId,
            new Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad,
                48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad),
            0.0, Aberration.CNS, TimeSpan.FromHours(4.5),
            Stars.Sun.NaifId).ToArray();
        Assert.NotEmpty(results);
    }

    [Fact]
    public void FindWindowsOnIlluminationConstraint_ObsoleteOverload()
    {
#pragma warning disable CS0618
        var searchWindow = new Window(Time.CreateTDB(674524800),
            Time.CreateTDB(674611200));
        var results = SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(
            searchWindow, Sun, EarthAtJ2000,
            new Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad,
                48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationnalOperator.Lower,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad),
            0.0, Aberration.CNS, TimeSpan.FromHours(4.5),
            Sun).ToArray();
        Assert.NotEmpty(results);
#pragma warning restore CS0618
    }

    // ===== FindWindowsInFieldOfViewConstraint =====

    [Fact]
    public void FindWindowsInFieldOfViewConstraint()
    {
        Clock clock = new Clock("smokeFovClk", 65536);
        var parkingOrbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.Create(676555130.0, TimeFrame.TDBFrame), Frame.ICRF);

        var spacecraft = new Spacecraft(-904, "SmokeFovSpc", 1000.0, 5000.0, clock, parkingOrbit);
        spacecraft.AddCircularInstrument(-904789, "SMOKECAM", "mod1", 0.75,
            Vector3.VectorZ, Vector3.VectorY,
            new Vector3(0.0, System.Math.PI * 0.5, 0.0));

        var searchWindow = new Window(
            Time.Create(676555200.0, TimeFrame.TDBFrame),
            Time.Create(676561646.0, TimeFrame.TDBFrame));

        var results = SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(
            searchWindow, spacecraft.NaifId, spacecraft.Instruments.First().NaifId,
            PlanetsAndMoons.EARTH.NaifId, EarthAtJ2000.Frame,
            ShapeType.Ellipsoid, Aberration.LT,
            TimeSpan.FromSeconds(360.0)).ToArray();

        // May or may not find windows depending on orbit geometry, but should not throw
        Assert.NotNull(results);
    }

    [Fact]
    public void FindWindowsInFieldOfViewConstraint_ObsoleteOverload()
    {
#pragma warning disable CS0618
        Clock clock = new Clock("smokeFovClk2", 65536);
        var parkingOrbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.Create(676555130.0, TimeFrame.TDBFrame), Frame.ICRF);

        var spacecraft = new Spacecraft(-905, "SmokeFovSpc2", 1000.0, 5000.0, clock, parkingOrbit);
        spacecraft.AddCircularInstrument(-905789, "SMOKECAM2", "mod1", 0.75,
            Vector3.VectorZ, Vector3.VectorY,
            new Vector3(0.0, System.Math.PI * 0.5, 0.0));

        var searchWindow = new Window(
            Time.Create(676555200.0, TimeFrame.TDBFrame),
            Time.Create(676561646.0, TimeFrame.TDBFrame));

        var results = SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(
            searchWindow, spacecraft, spacecraft.Instruments.First(),
            EarthAtJ2000, EarthAtJ2000.Frame,
            ShapeType.Ellipsoid, Aberration.LT,
            TimeSpan.FromSeconds(360.0)).ToArray();

        Assert.NotNull(results);
#pragma warning restore CS0618
    }
}
