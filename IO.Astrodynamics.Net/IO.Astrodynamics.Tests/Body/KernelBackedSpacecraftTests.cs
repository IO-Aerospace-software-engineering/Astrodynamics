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
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
