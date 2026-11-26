// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// Calls whose argument the native layer rejected with a C++ exception across P/Invoke, which ended the process (exit code
/// 134), and which the library now rejects before the native call (#560 and the comment on #454): the body information of
/// an identifier that is not a celestial body, and orientations in several frames. Each test is the call that ended the
/// test host before; it now throws and the host goes on.
/// </summary>
public class NativeAbortGuardTests
{
    private readonly ITestOutputHelper _output;

    public NativeAbortGuardTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private void AssertKernelGm(int id, double gm)
    {
        double expected = KernelTextValues.GravitationalParameter(id);
        long ulps = KernelTextValues.UlpDistance(expected, gm);
        _output.WriteLine($"MEASURE GM of {id} from gm_de440.tpc's text, ulp: {ulps}");
        Assert.True(ulps <= KernelTextValues.SpiceParseUlps, $"GM of {id}: {gm:R}, the kernel text gives {expected:R} ({ulps} ulp)");
    }

    /// <summary>
    /// Bodies SPICE names and <c>gm_de440.tpc</c> gives a GM, without <c>BODYnnn_RADII</c> in <c>pck00011.tpc</c>: the
    /// failed RADII read left SPICE failed, so the GM read returned 0 and the frame stayed unread or garbage (#672). The
    /// frames are those of CSPICE's <c>cnmfrm_c</c> called directly; none for Kerberos, Styx and the Didymos barycentre.
    /// </summary>
    [Theory]
    [InlineData(902, "IAU_NIX", 10108)]
    [InlineData(903, "IAU_HYDRA", 10109)]
    [InlineData(904, "", 0)]
    [InlineData(905, "", 0)]
    [InlineData(2000002, "IAU_PALLAS", 10102)]
    [InlineData(20065803, "", 0)]
    [InlineData(120065803, "IAU_DIMORPHOS", 10114)]
    [InlineData(920065803, "IAU_DIDYMOS", 10113)]
    public void GetCelestialBodyInfo_GmWithoutRadii_ReadsTheKernelGm(int id, string frameName, int frameId)
    {
        Assert.Empty(KernelTextValues.Radii(id));
        var first = SpiceApi.Instance.GetCelestialBodyInfo(id);
        Assert.Equal(id, first.NaifId);
        AssertKernelGm(id, first.GM);
        Assert.Equal(0.0, first.Radii.X);
        Assert.Equal(0.0, first.Radii.Y);
        Assert.Equal(0.0, first.Radii.Z);
        Assert.Equal(frameName, first.FrameName);
        Assert.Equal(frameId, first.FrameId);
        for (int i = 0; i < 2; i++)
        {
            var again = SpiceApi.Instance.GetCelestialBodyInfo(id);
            Assert.Equal(first.GM, again.GM);
            Assert.Equal(first.FrameName, again.FrameName);
            Assert.Equal(first.FrameId, again.FrameId);
        }
    }

    /// <summary>
    /// Bodies with <c>BODYnnn_RADII</c> in <c>pck00011.tpc</c> and no GM in <c>gm_de440.tpc</c> (39 of them): the failed GM
    /// read skipped the frame read, whose name was the stack's (#672). GM 0, the radii of the kernel text, the frame of
    /// <c>cnmfrm_c</c>.
    /// </summary>
    [Theory]
    [InlineData(507, "IAU_ELARA", 10029)]
    [InlineData(802, "IAU_NEREID", 10072)]
    public void GetCelestialBodyInfo_RadiiWithoutGm_ReadsTheFrame(int id, string frameName, int frameId)
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(id));
        var radii = KernelTextValues.Radii(id);
        for (int i = 0; i < 3; i++)
        {
            var info = SpiceApi.Instance.GetCelestialBodyInfo(id);
            Assert.Equal(id, info.NaifId);
            Assert.Equal(0.0, info.GM);
            Assert.Equal(radii[0], info.Radii.X);
            Assert.Equal(radii[1], info.Radii.Y);
            Assert.Equal(radii[2], info.Radii.Z);
            Assert.Equal(frameName, info.FrameName);
            Assert.Equal(frameId, info.FrameId);
        }
    }

    /// <summary>
    /// Identifiers of the celestial classes that SPICE does not name (<c>bodc2n_c</c> finds none; 2000003 has a GM in
    /// <c>gm_de440.tpc</c>; 398 is a moon's slot of the Earth): the record held the previous call's name or stack bytes and
    /// <c>Id</c> -1, and a body built from it had a garbage name and frame (#672). Each read follows a read of a named body.
    /// </summary>
    [Theory]
    [InlineData(398)]
    [InlineData(12345678)]
    [InlineData(2000003)]
    [InlineData(99999999)]
    public void GetCelestialBodyInfo_UnnamedIdentifier_Throws(int id)
    {
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal("PALLAS", SpiceApi.Instance.GetCelestialBodyInfo(2000002).Name);
            var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.GetCelestialBodyInfo(id));
            Assert.Equal("naifId", e.ParamName);
            Assert.Contains(id.ToString(), e.Message);
            Assert.Throws<ArgumentException>(() => new CelestialBody(id));
            Assert.Throws<ArgumentException>(() => CelestialItem.Create(id));
        }
    }

    /// <summary>
    /// The Lagrange points 391 to 395, which the solar system kernels do not name (<c>bodc2n_c</c> finds none of them), have
    /// no body information (#672), but <see cref="CelestialItem.Create"/> builds them as a <see cref="LagrangePoint"/>
    /// without reading it, as before. L3 (393) is left out: <c>Data/SolarSystem</c> has no SPK for it, and the
    /// <see cref="LagrangePoint"/> constructor reads its state at J2000 TDB.
    /// </summary>
    [Theory]
    [InlineData(391)]
    [InlineData(392)]
    [InlineData(394)]
    [InlineData(395)]
    public void LagrangePoint_TheKernelsDoNotName_IsBuiltByCreateOnly(int id)
    {
        var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.GetCelestialBodyInfo(id));
        Assert.Equal("naifId", e.ParamName);
        Assert.Throws<ArgumentException>(() => new CelestialBody(id));
        var point = Assert.IsType<LagrangePoint>(CelestialItem.Create(id));
        Assert.Equal(id, point.NaifId);
        Assert.Equal($"L{id - 390}", point.Name);
    }

    /// <summary>
    /// The observer of a read from L1 (391), which the kernels do not name, is still the <see cref="LagrangePoint"/> of
    /// <see cref="CelestialItem.Create"/> (#672 changes only the site identifiers), and its state the exact negation of the
    /// read from the Earth to L1 (a read with a celestial observer: <c>spkezr_c</c> subtracts the same two states from the
    /// solar system barycentre), on both overloads: 0 measured on Linux, Windows and macOS arm64 (CI run 43600640037,
    /// 2026-11-14).
    /// </summary>
    [Fact]
    public void ReadEphemeris_FromAnUnnamedLagrangePoint_IsALagrangePoint()
    {
        var epoch = new TimeSystem.Time(2023, 1, 1, 1, 0, 0, TimeScale.TDB);
        var reads = new[] { SpiceApi.Instance.ReadEphemeris(epoch, 391, 399, Frames.Frame.ICRF, Aberration.None) }
            .Concat(SpiceApi.Instance.ReadEphemeris(new Window(epoch, epoch.AddSeconds(60.0)), 391, 399, Frames.Frame.ICRF,
                Aberration.None, TimeSpan.FromSeconds(60.0)).Select(x => x.ToStateVector()))
            .ToArray();
        Assert.Equal(3, reads.Length);
        foreach (var read in reads)
        {
            var observer = Assert.IsType<LagrangePoint>(read.Observer);
            Assert.Equal(391, observer.NaifId);
            Assert.Equal("L1", observer.Name);
            var reversed = SpiceApi.Instance.ReadEphemeris(read.Epoch, 399, 391, Frames.Frame.ICRF, Aberration.None);
            // L1 is about 1.5e9 m from the Earth: the reference is a state, not the zeros of a read SPICE refused
            Assert.True(reversed.Position.Magnitude() > 1.0e9);
            double dr = (read.Position + reversed.Position).Magnitude();
            double dv = (read.Velocity + reversed.Velocity).Magnitude();
            _output.WriteLine($"MEASURE Lagrange observer |dr| m: {dr:E3}, |dv| m/s: {dv:E3}");
            Assert.Equal(0.0, dr);
            Assert.Equal(0.0, dv);
            LabelAssert.Equal(reversed.Epoch, read.Epoch);
        }
    }

    /// <summary>
    /// A barycentre has no body-fixed frame (<c>cnmfrm_c</c> finds none): its frame name was the stack's (#672). Its GM,
    /// for 1 to 9, the kernel text's.
    /// </summary>
    [Theory]
    [InlineData(0, "SOLAR SYSTEM BARYCENTER")]
    [InlineData(1, "MERCURY BARYCENTER")]
    [InlineData(2, "VENUS BARYCENTER")]
    [InlineData(3, "EARTH BARYCENTER")]
    [InlineData(4, "MARS BARYCENTER")]
    [InlineData(5, "JUPITER BARYCENTER")]
    [InlineData(6, "SATURN BARYCENTER")]
    [InlineData(7, "URANUS BARYCENTER")]
    [InlineData(8, "NEPTUNE BARYCENTER")]
    [InlineData(9, "PLUTO BARYCENTER")]
    public void GetCelestialBodyInfo_Barycenter_HasNoFrame(int id, string name)
    {
        // A body with a frame first, so that the stack holds its frame name
        Assert.Equal("ITRF93", SpiceApi.Instance.GetCelestialBodyInfo(399).FrameName);
        var info = SpiceApi.Instance.GetCelestialBodyInfo(id);
        Assert.Equal(id, info.NaifId);
        Assert.Equal(name, info.Name);
        Assert.Equal(string.Empty, info.FrameName);
        Assert.Equal(0, info.FrameId);
        if (id > 0) AssertKernelGm(id, info.GM);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Occultation_UnnamedEllipsoid_Throws(bool front)
    {
        // The frame of an ellipsoid read from the record of an unnamed identifier was the stack's (#672)
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddSeconds(3600.0));
        var e = Assert.Throws<ArgumentException>(() => front
            ? SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, 10, ShapeType.Ellipsoid, 12345678, ShapeType.Ellipsoid,
                OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0))
            : SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, 12345678, ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid,
                OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0)));
        Assert.Equal(front ? "frontBodyId" : "targetId", e.ParamName);
        Assert.Contains("12345678", e.Message);
    }

    /// <summary>
    /// -82 is CASSINI, a name built into SPICE (#560's measurement: exit code 134 on 0f0cf9eb); -999 has no name (an empty
    /// record before); 50, 100 and 1000 are no class of the native layer.
    /// </summary>
    [Theory]
    [InlineData(-82)]
    [InlineData(-999)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    public void GetCelestialBodyInfo_NotACelestialBody_Throws(int id)
    {
        var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.GetCelestialBodyInfo(id));
        Assert.Equal("naifId", e.ParamName);
        Assert.Contains(id.ToString(), e.Message);
        Assert.Throws<ArgumentException>(() => new CelestialBody(id));
    }

    [Theory]
    [InlineData(0, "SOLAR SYSTEM BARYCENTER")]
    [InlineData(3, "EARTH BARYCENTER")]
    [InlineData(10, "SUN")]
    [InlineData(301, "MOON")]
    [InlineData(399, "EARTH")]
    [InlineData(499, "MARS")]
    public void GetCelestialBodyInfo_ACelestialBody_IsRead(int id, string name)
    {
        var info = SpiceApi.Instance.GetCelestialBodyInfo(id);
        Assert.Equal(id, info.NaifId);
        Assert.Equal(name, info.Name);
    }

    [Fact]
    public void ReadEphemeris_ObserverNotACelestialBody_Throws()
    {
        // The observer of the reads by identifiers is built as a celestial body: a spacecraft identifier ended the process
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddSeconds(60.0));
        Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.ReadEphemeris(window, -82, 301, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(10.0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Occultation_EllipsoidOfASpacecraft_Throws(bool front)
    {
        // The frame of an ellipsoid read from the body information of a spacecraft identifier ended the process
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddSeconds(3600.0));
        var e = Assert.Throws<ArgumentException>(() => front
            ? SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, 10, ShapeType.Ellipsoid, -82, ShapeType.Ellipsoid,
                OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0))
            : SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, -82, ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid,
                OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0)));
        Assert.Equal(front ? "frontBodyId" : "targetId", e.ParamName);
    }

    /// <summary>
    /// Orientations in two frames (the comment on #454 of 2026-11-02: ten records, one in ECLIPJ2000, exit code 134): the
    /// native writer's frame check threw a C++ exception. Rejected before the native call, so no file is written.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void WriteOrientation_SeveralFrames_Throws(int eclipticIndex)
    {
        var file = new FileInfo(Path.Combine(Constants.OutputPath.FullName, $"several_frames_{eclipticIndex}.ck"));
        if (file.Exists) file.Delete();
        var records = new StateOrientation[10];
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0),
                Vector3.Zero, TimeSystem.Time.J2000Tdb.AddSeconds(i), i == eclipticIndex ? Frames.Frame.ECLIPJ2000 : Frames.Frame.ICRF);
        }

        var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.WriteOrientation(file, -1560, records));
        Assert.Equal("stateOrientations", e.ParamName);
        file.Refresh();
        Assert.False(file.Exists);
    }

    // Equally spaced states of a circular orbit about the Earth in ICRF, 60 s apart from J2000
    private static StateVector[] EarthStates(int count, ILocalizable centre = null) => Enumerable.Range(0, count)
        .Select(i => new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), centre ?? TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000Tdb.AddSeconds(60.0 * i), Frames.Frame.ICRF)).ToArray();

    /// <summary>
    /// The native writer of SPKs rejects fewer than three states (<c>EphemerisKernel::WriteData</c>, whose message said "2
    /// items or more"), states in several frames, and reads the centre of motion as a celestial body: each threw a C++
    /// exception across P/Invoke, which ended the process (#706, exit code 134 on c533a6e, the child scenarios of
    /// <see cref="KernelUnloadChildTests"/>), or for a spacecraft centre read the uninitialised GM of a failed read.
    /// Rejected before the native call with an <see cref="ArgumentException"/> naming <c>stateVectors</c>: a new path is not
    /// written, and an existing file keeps its bytes. Reference: the native rule itself.
    /// </summary>
    private static void AssertWriteEphemerisRejected(StateVector[] states, string contains)
    {
        var folder = Directory.CreateTempSubdirectory("native-abort-guard-");
        try
        {
            var fresh = new FileInfo(Path.Combine(folder.FullName, "fresh.bsp"));
            var existing = new FileInfo(Path.Combine(folder.FullName, "existing.bsp"));
            SpiceApi.Instance.WriteEphemeris(existing, -1999720, EarthStates(3));
            var bytes = File.ReadAllBytes(existing.FullName);

            var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.WriteEphemeris(fresh, -1999720, states));
            Assert.Equal("stateVectors", e.ParamName);
            Assert.Contains(contains, e.Message);
            Assert.False(File.Exists(fresh.FullName));
            e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.WriteEphemeris(existing, -1999720, states));
            Assert.Equal("stateVectors", e.ParamName);
            Assert.Equal(bytes, File.ReadAllBytes(existing.FullName));
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WriteEphemeris_FewerThanThreeStates_Throws(int count) => AssertWriteEphemerisRejected(EarthStates(count), "three states");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WriteEphemeris_SeveralFrames_Throws(int eclipticIndex)
    {
        var states = EarthStates(3);
        states[eclipticIndex] = new StateVector(states[eclipticIndex].Position, states[eclipticIndex].Velocity, states[eclipticIndex].Observer,
            states[eclipticIndex].Epoch, Frames.Frame.ECLIPJ2000);
        AssertWriteEphemerisRejected(states, "frame of the first one");
    }

    [Fact]
    public void WriteEphemeris_CentreNotACelestialBody_Throws()
    {
        var spacecraft = new Spacecraft(-1999721, "IOA_K1_GUARD", 3000.0, EarthStates(1)[0], 10000.0, new Clock("IOA_K1_GUARD_CLOCK", 65536));
        AssertWriteEphemerisRejected(EarthStates(3, spacecraft), "not a celestial body");
        var site = new Site(339, "S339", TestHelpers.EarthAtJ2000, new Planetodetic(0.5, 0.2, 1000.0));
        AssertWriteEphemerisRejected(EarthStates(3, site), "not a celestial body");
    }

    /// <summary>
    /// A site over 90 s: its 1-minute step gives two states, which the native writer rejected with a C++ exception (#706,
    /// exit code 134 on c533a6e). The <see cref="ArgumentException"/> of <see cref="SpiceApi.WriteEphemeris"/> reaches the
    /// caller; over two minutes, three states, the site is written.
    /// </summary>
    [Fact]
    public void Site_WriteEphemeris_WindowOfTwoStates_Throws()
    {
        var site = new Site(339, "S339", TestHelpers.EarthAtJ2000, new Planetodetic(0.5, 0.2, 1000.0));
        var folder = Directory.CreateTempSubdirectory("native-abort-guard-");
        try
        {
            var file = new FileInfo(Path.Combine(folder.FullName, "site.bsp"));
            var start = TimeSystem.Time.J2000Tdb;
            var e = Assert.Throws<ArgumentException>(() => site.WriteEphemeris(new Window(start, start.AddSeconds(90.0)), file));
            Assert.Equal("stateVectors", e.ParamName);
            Assert.False(File.Exists(file.FullName));
            site.WriteEphemeris(new Window(start, start.AddSeconds(120.0)), file);
            Assert.True(File.Exists(file.FullName));
        }
        finally
        {
            folder.Delete(true);
        }
    }
}
