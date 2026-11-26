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
/// Comprehensive smoke tests exercising every public SpiceApi method.
/// These tests verify that the native P/Invoke layer is correctly loaded
/// and all entry points are reachable.
/// </summary>
[Collection(KernelCollection.Name)]
public class SpiceAPISmokeTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public SpiceAPISmokeTests()
    {
        SpiceApi.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static CelestialBody EarthAtJ2000 =>
        new(PlanetsAndMoons.Earth, Frame.ICRF, new Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB));

    private static CelestialBody MoonAtJ2000 =>
        new(PlanetsAndMoons.Moon, Frame.ICRF, new Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB));

    private static CelestialBody Sun => new(Stars.Sun);

    // ===== Kernel Management =====

    [Fact]
    public void GetSpiceVersion_ReturnsNonEmpty()
    {
        var version = SpiceApi.Instance.GetSpiceVersion();
        Assert.False(string.IsNullOrEmpty(version));
    }

    [Fact]
    public void LoadKernels_ReturnsLoadedKernels()
    {
        var kernels = SpiceApi.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void LoadAndUnloadKernels()
    {
        var tempDir = new DirectoryInfo(SolarSystemKernelPath.FullName);
        // Kernels are already loaded in constructor; just verify unload/reload cycle works
        SpiceApi.Instance.UnloadKernels(tempDir);
        SpiceApi.Instance.LoadKernels(tempDir);
        var kernels = SpiceApi.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    [Fact]
    public void ClearKernels_ThenReload()
    {
        SpiceApi.Instance.ClearKernels();
        // Reload for subsequent tests
        SpiceApi.Instance.LoadKernels(SolarSystemKernelPath);
        var kernels = SpiceApi.Instance.GetLoadedKernels();
        Assert.NotEmpty(kernels);
    }

    // ===== GetCelestialBodyInfo =====

    [Fact]
    public void GetCelestialBodyInfo_Earth()
    {
        var info = SpiceApi.Instance.GetCelestialBodyInfo(PlanetsAndMoons.Earth.NaifId);
        Assert.Equal(399, info.NaifId);
        Assert.Equal("EARTH", info.Name);
        Assert.True(info.GM > 0);
        Assert.True(info.Radii.X > 0);
    }

    [Fact]
    public void GetCelestialBodyInfo_Moon()
    {
        var info = SpiceApi.Instance.GetCelestialBodyInfo(PlanetsAndMoons.Moon.NaifId);
        Assert.Equal(301, info.NaifId);
        Assert.Equal("MOON", info.Name);
        Assert.True(info.GM > 0);
    }

    // ===== ReadEphemeris =====

    [Fact]
    public void ReadEphemeris_Windowed()
    {
        var searchWindow = new Window(Time.CreateTdb(0.0), Time.CreateTdb(100.0));
        var results = SpiceApi.Instance.ReadEphemeris(searchWindow, PlanetsAndMoons.Earth.NaifId,
                PlanetsAndMoons.Moon.NaifId, Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(10.0))
            .ToArray();

        Assert.NotEmpty(results);
        Assert.NotEqual(0.0, results[0].Position.Magnitude());
        Assert.NotEqual(0.0, results[0].Velocity.Magnitude());
        Assert.Equal(PlanetsAndMoons.Earth.NaifId, results[0].Observer.NaifId);
        Assert.Equal(Frame.ICRF, results[0].Frame);
    }

    [Fact]
    public void ReadEphemeris_SingleEpoch()
    {
        var epoch = Time.CreateTdb(0.0);
        var sv = SpiceApi.Instance.ReadEphemeris(epoch, PlanetsAndMoons.Earth.NaifId,
            PlanetsAndMoons.Moon.NaifId, Frame.ICRF, Aberration.LT);

        Assert.NotEqual(0.0, sv.Position.Magnitude());
        Assert.Equal(PlanetsAndMoons.Earth.NaifId, sv.Observer.NaifId);
    }

    [Fact]
    public void ReadEphemeris_SsbObserver()
    {
        var epoch = Time.CreateTdb(0.0);
        var sv = SpiceApi.Instance.ReadEphemeris(epoch, 0, PlanetsAndMoons.Earth.NaifId,
            Frame.ICRF, Aberration.None);
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
                EarthAtJ2000, Time.CreateTdb(i), Frame.ICRF);
        }

        var file = new FileInfo("SmokeTest_Ephemeris.spk");
        try
        {
            SpiceApi.Instance.WriteEphemeris(file, -900, sv);
            SpiceApi.Instance.LoadKernels(file);

            var window = new Window(Time.J2000Tdb, Time.J2000Tdb.AddSeconds(9.0));
            var results = SpiceApi.Instance.ReadEphemeris(window, PlanetsAndMoons.Earth.NaifId, -900,
                    Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(1.0))
                .ToArray();
            Assert.Equal(size, results.Length);
            Assert.Equal(6800000.0, results[0].Position.X, 3);
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(file);
            if (file.Exists) file.Delete();
        }
    }

    // ===== WriteOrientation & ReadOrientation =====

    [Fact]
    public void WriteAndReadOrientation()
    {
        const int size = 10;
        Clock clock = new Clock("smokeClk2", 65536);
        var spacecraft = new Spacecraft(-902, "SmokeSpc2", 1000.0, new StateVector(new Vector3(6800000, 0, 0), new Vector3(0, 7656.0, 0),
                EarthAtJ2000, Time.CreateTdb(0.0), Frame.ICRF), 5000.0,
            clock);

        var so = new StateOrientation[size];
        for (int i = 0; i < size; ++i)
        {
            so[i] = new StateOrientation(
                new Quaternion(i, 1 + i * 0.1, 1 + i * 0.2, 1 + i * 0.3),
                Vector3.Zero, Time.CreateTdb(i), Frame.ICRF);
        }

        var clockFile = new FileInfo("SmokeTest_Clock.tsc");
        var ckFile = new FileInfo("SmokeTest_Orientation.ck");
        try
        {
            clock.Write(clockFile);
            SpiceApi.Instance.LoadKernels(clockFile);

            SpiceApi.Instance.WriteOrientation(ckFile, spacecraft.NaifId, so);
            SpiceApi.Instance.LoadKernels(ckFile);

            var window = new Window(Time.J2000Tdb, Time.J2000Tdb.AddSeconds(9.0));
            var results = SpiceApi.Instance.ReadOrientation(window, spacecraft.NaifId,
                TimeSpan.Zero, Frame.ICRF, TimeSpan.FromSeconds(1.0)).ToArray();

            Assert.NotEmpty(results);
            Assert.Equal(Frame.ICRF, results[0].ReferenceFrame);
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(ckFile);
            SpiceApi.Instance.UnloadKernels(clockFile);
            if (ckFile.Exists) ckFile.Delete();
            if (clockFile.Exists) clockFile.Delete();
        }
    }

    // ===== TransformFrame =====

    [Fact]
    public void TransformFrame_SingleEpoch()
    {
        var result = SpiceApi.Instance.TransformFrame(Time.J2000Tdb, Frame.ICRF,
            new Frame("ITRF93"));

        Assert.NotEqual(0.0, result.Rotation.W);
        Assert.NotEqual(0.0, result.AngularVelocity.Magnitude());
    }

    [Fact]
    public void TransformFrame_Windowed()
    {
        var window = new Window(Time.J2000Tdb, Time.J2000Tdb.AddDays(1.0));
        var results = SpiceApi.Instance.TransformFrame(window, Frame.ICRF,
            new Frame("ITRF93"), TimeSpan.FromDays(0.5)).ToArray();

        Assert.True(results.Length >= 2);
        Assert.NotEqual(results[0].Rotation.W, results[1].Rotation.W);
    }

    // ===== FindWindowsOnDistanceConstraint =====

    [Fact]
    public void FindWindowsOnDistanceConstraint()
    {
        var searchWindow = new Window(Time.CreateTdb(220881665.18391809),
            Time.CreateTdb(228657665.18565452));
        var results = SpiceApi.Instance.FindWindowsOnDistanceConstraint(
            searchWindow, PlanetsAndMoons.Earth.NaifId, PlanetsAndMoons.Moon.NaifId,
            RelationalOperator.GreaterThan, 400000000, Aberration.None,
            TimeSpan.FromSeconds(86400.0)).ToArray();
        Assert.NotEmpty(results);
    }

    // ===== FindWindowsOnOccultationConstraint =====

    [Fact]
    public void FindWindowsOnOccultationConstraint()
    {
        var searchWindow = new Window(Time.CreateTdb(61473664.183390938),
            Time.CreateTdb(61646464.183445148));
        var results = SpiceApi.Instance.FindWindowsOnOccultationConstraint(
            searchWindow, PlanetsAndMoons.Earth.NaifId, Stars.Sun.NaifId,
            ShapeType.Ellipsoid, PlanetsAndMoons.Moon.NaifId, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.LT,
            TimeSpan.FromSeconds(3600.0)).ToArray();
        Assert.NotEmpty(results);
    }

    // ===== FindWindowsOnCoordinateConstraint =====

    [Fact]
    public void FindWindowsOnCoordinateConstraint()
    {
        var site = new Site(13, "DSS-13", EarthAtJ2000,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad,
                35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad, 0.107));

        var searchWindow = new Window(Time.CreateTdb(730036800.0),
            Time.CreateTdb(730123200));
        var results = SpiceApi.Instance.FindWindowsOnCoordinateConstraint(
            searchWindow, site.NaifId, PlanetsAndMoons.Moon.NaifId,
            site.Frame, CoordinateSystem.Rectangular, Coordinate.Z,
            RelationalOperator.GreaterThan, 0.0, 0.0, Aberration.None,
            TimeSpan.FromSeconds(60.0)).ToArray();
        Assert.NotEmpty(results);
    }

    // ===== FindWindowsOnIlluminationConstraint =====

    [Fact]
    public void FindWindowsOnIlluminationConstraint()
    {
        var searchWindow = new Window(Time.CreateTdb(674524800),
            Time.CreateTdb(674611200));
        var results = SpiceApi.Instance.FindWindowsOnIlluminationConstraint(
            searchWindow, Stars.Sun.NaifId, PlanetsAndMoons.Earth.NaifId,
            new Frame("ITRF93"),
            new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad,
                48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
            IlluminationAngle.Incidence, RelationalOperator.LessThan,
            System.Math.PI * 0.5 - (-0.8 * IO.Astrodynamics.Constants.Deg2Rad),
            0.0, Aberration.CnS, TimeSpan.FromHours(4.5),
            Stars.Sun.NaifId).ToArray();
        Assert.NotEmpty(results);
    }

    // ===== FindWindowsInFieldOfViewConstraint =====

    [Fact]
    public void FindWindowsInFieldOfViewConstraint()
    {
        Clock clock = new Clock("smokeFovClk", 65536);
        var parkingOrbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            EarthAtJ2000, Time.Create(676555130.0, TimeScale.TDB), Frame.ICRF);

        var spacecraft = new Spacecraft(-904, "SmokeFovSpc", 1000.0, parkingOrbit, 5000.0, clock);
        spacecraft.AddCircularInstrument("SMOKECAM", 0.75,
            Vector3.VectorZ, Vector3.VectorY,
            new Vector3(0.0, System.Math.PI * 0.5, 0.0), naifId: -904789, model: "mod1");

        var searchWindow = new Window(
            Time.Create(676555200.0, TimeScale.TDB),
            Time.Create(676561646.0, TimeScale.TDB));

        var results = SpiceApi.Instance.FindWindowsInFieldOfViewConstraint(
            searchWindow, spacecraft.NaifId, spacecraft.Instruments.First().NaifId,
            PlanetsAndMoons.Earth.NaifId, EarthAtJ2000.Frame,
            ShapeType.Ellipsoid, Aberration.LT,
            TimeSpan.FromSeconds(360.0)).ToArray();

        // May or may not find windows depending on orbit geometry, but should not throw
        Assert.NotNull(results);
    }

}
