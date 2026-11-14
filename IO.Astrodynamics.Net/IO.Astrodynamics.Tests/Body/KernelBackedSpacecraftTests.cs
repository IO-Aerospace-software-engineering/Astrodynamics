using System;
using System.IO;
using System.Linq;
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

namespace IO.Astrodynamics.Tests.Body;

public class KernelBackedSpacecraftTests
{
    public KernelBackedSpacecraftTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    #region IsSpiceBacked property tests

    [Fact]
    public void CelestialBodyIsSpiceBacked()
    {
        Assert.True(TestHelpers.EarthAtJ2000.IsSpiceBacked);
    }

    [Fact]
    public void BarycenterIsSpiceBacked()
    {
        Assert.True(TestHelpers.SsbAtJ2000.IsSpiceBacked);
    }

    [Fact]
    public void StarIsNotSpiceBacked()
    {
        var star = new Star(1, "TestStar", 1.989e30, "G2V", 4.83, 0.772,
            new Equatorial(0.0, 0.0, 1.0, TimeSystem.Time.J2000TDB),
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, TimeSystem.Time.J2000TDB);
        Assert.False(star.IsSpiceBacked);
    }

    [Fact]
    public void PropagatedSpacecraftIsNotSpiceBacked()
    {
        var sc = TestHelpers.Spacecraft;
        Assert.False(sc.IsSpiceBacked);
        Assert.False(sc.IsPropagated);
    }

    [Fact]
    public void KernelSiteIsSpiceBacked()
    {
        // DSS-13 is a kernel-backed site (no planetodetic coords → reads from kernel)
        var site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
        Assert.True(site.IsSpiceBacked);
    }

    [Fact]
    public void CustomSiteIsNotSpiceBacked()
    {
        var site = new Site(339, "CustomSite", TestHelpers.EarthAtJ2000,
            new Planetodetic(30 * Astrodynamics.Constants.Deg2Rad, 10 * Astrodynamics.Constants.Deg2Rad, 1000.0));
        Assert.False(site.IsSpiceBacked);
    }

    #endregion

    #region Kernel-backed Spacecraft construction and ephemeris

    [Fact]
    public void KernelBackedSpacecraftConstruction()
    {
        // Write a small SPK for a test spacecraft
        var spkFile = CreateTestSpk(-9901, "KernelSC_Construct");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9901, "KernelSC_Construct",
                epoch: TimeSystem.Time.J2000TDB);

            Assert.True(sc.IsSpiceBacked);
            Assert.False(sc.IsPropagated);
            Assert.Equal(-9901, sc.NaifId);
            Assert.Equal("KernelSC_Construct", sc.Name);
            Assert.NotNull(sc.InitialOrbitalParameters);
            Assert.NotNull(sc.Clock);
            Assert.NotNull(sc.Frame);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftIsSpiceBacked()
    {
        var spkFile = CreateTestSpk(-9902, "KernelSC_SpiceBacked");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9902, "KernelSC_SpiceBacked",
                epoch: TimeSystem.Time.J2000TDB);

            Assert.True(sc.IsSpiceBacked);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftGetEphemerisMatchesSpice()
    {
        var spkFile = CreateTestSpk(-9903, "KernelSC_Eph");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9903, "KernelSC_Eph",
                epoch: TimeSystem.Time.J2000TDB);

            var epoch = TimeSystem.Time.J2000TDB.Add(TimeSpan.FromSeconds(5));
            var earth = TestHelpers.EarthAtJ2000;

            // Get ephemeris via kernel-backed SC
            var scEphemeris = sc.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            // Get ephemeris directly from SPICE
            var spiceEphemeris = SpiceAPI.Instance.ReadEphemeris(epoch, earth.NaifId, sc.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            // Should match to machine precision
            Assert.Equal(spiceEphemeris.Position.X, scEphemeris.Position.X, 6);
            Assert.Equal(spiceEphemeris.Position.Y, scEphemeris.Position.Y, 6);
            Assert.Equal(spiceEphemeris.Position.Z, scEphemeris.Position.Z, 6);
            Assert.Equal(spiceEphemeris.Velocity.X, scEphemeris.Velocity.X, 6);
            Assert.Equal(spiceEphemeris.Velocity.Y, scEphemeris.Velocity.Y, 6);
            Assert.Equal(spiceEphemeris.Velocity.Z, scEphemeris.Velocity.Z, 6);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftGetGeometricStateRelativeToMatchesSpice()
    {
        var spkFile = CreateTestSpk(-9904, "KernelSC_GeoState");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9904, "KernelSC_GeoState",
                epoch: TimeSystem.Time.J2000TDB);

            var epoch = TimeSystem.Time.J2000TDB.Add(TimeSpan.FromSeconds(5));
            var earth = TestHelpers.EarthAtJ2000;

            // Get geometric state via kernel-backed SC
            var scState = sc.GetGeometricStateRelativeTo(epoch, earth);

            // Get state directly from SPICE
            var spiceState = SpiceAPI.Instance.ReadEphemeris(epoch, earth.NaifId, sc.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            Assert.Equal(spiceState.Position.X, scState.Position.X, 6);
            Assert.Equal(spiceState.Position.Y, scState.Position.Y, 6);
            Assert.Equal(spiceState.Position.Z, scState.Position.Z, 6);
            Assert.Equal(spiceState.Velocity.X, scState.Velocity.X, 6);
            Assert.Equal(spiceState.Velocity.Y, scState.Velocity.Y, 6);
            Assert.Equal(spiceState.Velocity.Z, scState.Velocity.Z, 6);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    #endregion

    #region Mutation guards

    [Fact]
    public void KernelBackedSpacecraftPropagateThrows()
    {
        var spkFile = CreateTestSpk(-9905, "KernelSC_PropThrow");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9905, "KernelSC_PropThrow",
                epoch: TimeSystem.Time.J2000TDB);

            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.Add(TimeSpan.FromHours(1)));
            Assert.Throws<InvalidOperationException>(() =>
                sc.Propagate(window, [TestHelpers.EarthAtJ2000], false, false, TimeSpan.FromSeconds(1)));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftPropagateAsyncThrows()
    {
        var spkFile = CreateTestSpk(-9906, "KernelSC_PropAsyncThrow");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9906, "KernelSC_PropAsyncThrow",
                epoch: TimeSystem.Time.J2000TDB);

            var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.Add(TimeSpan.FromHours(1)));
            // PropagateAsync throws synchronously before creating the Task
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = sc.PropagateAsync(window, [TestHelpers.EarthAtJ2000], false, false, TimeSpan.FromSeconds(1));
            });
            Assert.Contains("kernel-backed", ex.Message);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftWriteEphemerisThrows()
    {
        var spkFile = CreateTestSpk(-9907, "KernelSC_WriteThrow");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9907, "KernelSC_WriteThrow",
                epoch: TimeSystem.Time.J2000TDB);

            Assert.Throws<InvalidOperationException>(() =>
                sc.WriteEphemeris(new FileInfo("dummy.spk")));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftAddPropagatedStatesThrows()
    {
        var spkFile = CreateTestSpk(-9908, "KernelSC_AddStatesThrow");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9908, "KernelSC_AddStatesThrow",
                epoch: TimeSystem.Time.J2000TDB);

            var sv = new StateVector(Vector3.VectorX, Vector3.VectorY, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            Assert.Throws<InvalidOperationException>(() => sc.AddPropagatedStates(sv));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelBackedSpacecraftPositiveNaifIdThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Spacecraft(100, "BadSC", epoch: TimeSystem.Time.J2000TDB));
    }

    #endregion

    #region Mixed observer/target scenarios

    [Fact]
    public void KernelScAsObserverOfCelestialBody()
    {
        var spkFile = CreateTestSpk(-9909, "KernelSC_Observer");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var sc = new Spacecraft(-9909, "KernelSC_Observer",
                epoch: TimeSystem.Time.J2000TDB);

            var epoch = TimeSystem.Time.J2000TDB.Add(TimeSpan.FromSeconds(5));
            var moon = TestHelpers.MoonAtJ2000;

            // Moon ephemeris as seen from kernel SC observer — uses SPICE direct path
            var moonFromSc = moon.GetEphemeris(epoch, sc, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            Assert.NotEqual(0.0, moonFromSc.Position.Magnitude());
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    [Fact]
    public void KernelScWithNonSpiceObserver()
    {
        var spkFile = CreateTestSpk(-9910, "KernelSC_NonSpiceObs");

        try
        {
            SpiceAPI.Instance.LoadKernels(spkFile);

            var kernelSc = new Spacecraft(-9910, "KernelSC_NonSpiceObs",
                epoch: TimeSystem.Time.J2000TDB);

            // Propagated SC as observer → uses reference-body approach
            var propagatedSc = TestHelpers.Spacecraft;

            var epoch = TimeSystem.Time.J2000TDB.Add(TimeSpan.FromSeconds(5));
            var ephemeris = kernelSc.GetEphemeris(epoch, propagatedSc, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            Assert.NotEqual(0.0, ephemeris.Position.Magnitude());
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spkFile);
        }
    }

    #endregion

    #region Kernel-backed frame of a propagated spacecraft (#676)

    // The CK against the attitude record in 2024: the accuracy of a type-3 CK of the library at that epoch
    // (docs/reference/spacecraft.md, attitude and kernels, 2024 row)
    private const double CkAccuracy2024 = 6.1e-10;

    private static readonly TimeSystem.Time KSatEpoch = new(2024, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
    private static readonly TimeSystem.Time KSatRead = KSatEpoch.AddSeconds(1800.0);

    /// <summary>
    /// KSAT (-1401), propagated 2 h about the Earth in LVLH with its camera (-1401001), and the kernels the library writes
    /// of it: its SCLK, FK (the frame of id -1401000 renamed <paramref name="frameName"/> when given, as a mission FK
    /// names it), instrument FK and IK, SPK and CK. The kernels named by <paramref name="load"/> are loaded; each loaded
    /// kernel is unloaded by <see cref="Dispose"/>.
    /// </summary>
    private sealed class KSatKernels : IDisposable
    {
        private readonly System.Collections.Generic.List<FileInfo> _loaded = new();

        public Spacecraft Spacecraft { get; }
        public FileInfo Clock { get; }
        public FileInfo FrameKernel { get; }
        public FileInfo InstrumentFrame { get; }
        public FileInfo InstrumentKernel { get; }
        public FileInfo Ephemeris { get; }
        public FileInfo Orientation { get; }

        public KSatKernels(string frameName = null)
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            Spacecraft = new Spacecraft(-1401, "KSat", 100.0, 1000.0, new Clock("KSatClk", 256),
                new KeplerianElements(7000e3, 0.001, 0.9, 0.0, 0.0, 0.0, earth, KSatEpoch, Frames.Frame.ICRF));
            Spacecraft.AddCircularInstrument(-1401001, "Camera", "Imager", 0.1, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.1, 0.2, 0.3));
            var tank = new FuelTank("t", "m", "s", 100.0, 100.0);
            Spacecraft.AddFuelTank(tank);
            var engine = new Engine("e", "m", "s", 300.0, 1.0, tank);
            Spacecraft.AddEngine(engine);
            Spacecraft.SetStandbyManeuver(Maneuver.TriadAttitude.CreateLVLH(earth, KSatEpoch, TimeSpan.Zero, engine));
            Spacecraft.Propagate(new Window(KSatEpoch, KSatEpoch.AddSeconds(7200.0)), [earth],
                new Propagator.Integrators.RK78Integrator(1e-10, 1e-10), false, false, TimeSpan.FromSeconds(60.0));

            var directory = Constants.OutputPath.CreateSubdirectory("KernelBackedFrame676").CreateSubdirectory(Guid.NewGuid().ToString("N")[..8]);
            Clock = new FileInfo(Path.Combine(directory.FullName, "ksat.tsc"));
            FrameKernel = new FileInfo(Path.Combine(directory.FullName, "ksat.tf"));
            InstrumentFrame = new FileInfo(Path.Combine(directory.FullName, "camera.tf"));
            InstrumentKernel = new FileInfo(Path.Combine(directory.FullName, "camera.ti"));
            Ephemeris = new FileInfo(Path.Combine(directory.FullName, "ksat.bsp"));
            Orientation = new FileInfo(Path.Combine(directory.FullName, "ksat.bc"));
            Spacecraft.Clock.WriteAsync(Clock).GetAwaiter().GetResult();
            Spacecraft.Frame.WriteAsync(FrameKernel).GetAwaiter().GetResult();
            if (frameName != null)
            {
                File.WriteAllText(FrameKernel.FullName, File.ReadAllText(FrameKernel.FullName).Replace("KSAT_FRAME", frameName));
            }

            var instrument = Spacecraft.Instruments.Single();
            instrument.WriteFrameAsync(InstrumentFrame).GetAwaiter().GetResult();
            instrument.WriteKernelAsync(InstrumentKernel).GetAwaiter().GetResult();
            Spacecraft.WriteEphemeris(Ephemeris);
            Load(Clock);
            Spacecraft.WriteOrientation(Orientation);
        }

        public void Load(FileInfo kernel)
        {
            // FileInfo caches Exists, and LoadKernels loads nothing for a path it believes absent
            kernel.Refresh();
            Assert.True(kernel.Exists, kernel.FullName);
            SpiceAPI.Instance.LoadKernels(kernel);
            _loaded.Add(kernel);
        }

        public void Dispose()
        {
            for (int i = _loaded.Count - 1; i >= 0; i--)
            {
                SpiceAPI.Instance.UnloadKernels(_loaded[i]);
            }
        }
    }

    /// <summary>
    /// Control (#676, passes on main too): the instrument id next to the spacecraft frame id, -1401001. The written FK, IK
    /// and CK load, and SPICE's rotation of the instrument frame to J2000 (<c>sxform_c</c> on the FK's frame name) equals
    /// the managed instrument axes within the CK's 2024 accuracy.
    /// </summary>
    [Fact]
    public void InstrumentNextToFrameId_KernelsRead()
    {
        using var kernels = new KSatKernels();
        foreach (var kernel in new[] { kernels.FrameKernel, kernels.InstrumentFrame, kernels.InstrumentKernel, kernels.Ephemeris, kernels.Orientation })
        {
            kernels.Load(kernel);
        }

        // Act
        var instrument = kernels.Spacecraft.Instruments.Single();
        var spice = SpiceAPI.Instance.TransformFrame(KSatRead, new Frames.Frame("KSAT_CAMERA"), Frames.Frame.ICRF).Rotation;
        var managed = kernels.Spacecraft.Frame.GetStateOrientationToICRF(KSatRead).Rotation * instrument.RotationToSpacecraft;

        // Assert
        double angle = AttitudeRecordTests.RotationAngle(spice, managed);
        Assert.True(angle <= CkAccuracy2024, $"{angle:E3} rad");
    }

    /// <summary>
    /// A twin of another name than the propagated spacecraft reads the frame SPICE defines for its id, NaifId × 1000
    /// (#676): its rotation is SPICE's through the FK's own frame name (0 rad) and the managed record's within the CK's
    /// 2024 accuracy. On main the twin asked SPICE for SatK_FRAME, a frame it does not know, and threw.
    /// </summary>
    [Fact]
    public void TwinOfAnotherName_ReadsFrameOfItsId()
    {
        using var kernels = new KSatKernels();
        foreach (var kernel in new[] { kernels.FrameKernel, kernels.Ephemeris, kernels.Orientation })
        {
            kernels.Load(kernel);
        }

        // Act
        var twin = new Spacecraft(-1401, "SatK", KSatEpoch, clock: new Clock("SatKClk", 256));
        var read = twin.Frame.GetStateOrientationToICRF(KSatRead).Rotation;

        // Assert: the twin's frame keeps its name
        Assert.Equal("SatK_FRAME", twin.Frame.Name);
        var spice = SpiceAPI.Instance.TransformFrame(KSatRead, new Frames.Frame("KSAT_FRAME"), Frames.Frame.ICRF).Rotation;
        Assert.Equal(0.0, AttitudeRecordTests.RotationAngle(spice, read));
        var orientation = twin.GetOrientation(Frames.Frame.ICRF, KSatRead);
        var managed = kernels.Spacecraft.GetOrientation(Frames.Frame.ICRF, KSatRead);
        double angle = AttitudeRecordTests.RotationAngle(managed.Rotation, orientation.Rotation);
        Assert.True(angle <= CkAccuracy2024, $"{angle:E3} rad");
    }

    /// <summary>
    /// A mission FK names the frame of id -1401000 KSAT_BUS (#676): the twin KSat reads it, at 0 rad from SPICE's rotation
    /// through that name, also when the FK is loaded after the twin is built. On main it asked for KSAT_FRAME and threw.
    /// </summary>
    [Fact]
    public void MissionFrameName_ReadsFrameOfItsId()
    {
        using var kernels = new KSatKernels("KSAT_BUS");
        kernels.Load(kernels.Ephemeris);
        kernels.Load(kernels.Orientation);
        var twin = new Spacecraft(-1401, "KSat", KSatEpoch, clock: new Clock("KSatTwinClk", 256));

        // Act: the FK loaded after the twin is built
        kernels.Load(kernels.FrameKernel);
        var read = twin.Frame.GetStateOrientationToICRF(KSatRead).Rotation;

        // Assert
        var spice = SpiceAPI.Instance.TransformFrame(KSatRead, new Frames.Frame("KSAT_BUS"), Frames.Frame.ICRF).Rotation;
        Assert.Equal(0.0, AttitudeRecordTests.RotationAngle(spice, read));
        Assert.Equal("KSat_FRAME", twin.Frame.Name);
        double angle = AttitudeRecordTests.RotationAngle(kernels.Spacecraft.Frame.GetStateOrientationToICRF(KSatRead).Rotation, read);
        Assert.True(angle <= CkAccuracy2024, $"{angle:E3} rad");
    }

    /// <summary>
    /// No FK defines the frame of id -1401000: the read throws, naming the frame, as on main (#676).
    /// </summary>
    [Fact]
    public void NoFrameForId_ThrowsAsBefore()
    {
        using var kernels = new KSatKernels();
        kernels.Load(kernels.Ephemeris);
        kernels.Load(kernels.Orientation);
        var twin = new Spacecraft(-1401, "SatK", KSatEpoch, clock: new Clock("SatKClk", 256));

        // Act
        var exception = Record.Exception(() => twin.Frame.GetStateOrientationToICRF(KSatRead));

        // Assert
        var rejected = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("SatK_FRAME", rejected.Message);
    }

    #endregion

    #region Helper

    /// <summary>
    /// Creates a test SPK file with synthetic state vectors for a spacecraft.
    /// The SPK covers J2000 ± 100 seconds with Earth as center body.
    /// </summary>
    private static FileInfo CreateTestSpk(int naifId, string name)
    {
        var outputDir = Constants.OutputPath.CreateSubdirectory("KernelSC");
        var spkFile = new FileInfo(Path.Combine(outputDir.FullName, $"{name}_{naifId}.spk"));

        const int size = 201;
        var states = new StateVector[size];
        var baseEpoch = TimeSystem.Time.J2000TDB.Add(TimeSpan.FromSeconds(-100));

        for (int i = 0; i < size; i++)
        {
            var epoch = baseEpoch.Add(TimeSpan.FromSeconds(i));
            // Simple circular orbit at 6800 km radius, ~7.656 km/s velocity
            double t = i - 100; // centered on J2000
            double angle = t * 0.001; // slow rotation
            double r = 6800000.0; // 6800 km in meters
            double v = 7656.0; // ~7.656 km/s in m/s

            states[i] = new StateVector(
                new Vector3(r * System.Math.Cos(angle), r * System.Math.Sin(angle), 0),
                new Vector3(-v * System.Math.Sin(angle), v * System.Math.Cos(angle), 0),
                TestHelpers.EarthAtJ2000, epoch, Frames.Frame.ICRF);
        }

        SpiceAPI.Instance.WriteEphemeris(spkFile, naifId, states);
        return spkFile;
    }

    #endregion
}
