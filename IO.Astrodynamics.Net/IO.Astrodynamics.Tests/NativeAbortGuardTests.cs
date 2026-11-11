// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
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
    public NativeAbortGuardTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.GetCelestialBodyInfo(id));
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
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(id);
        Assert.Equal(id, info.Id);
        Assert.Equal(name, info.Name);
    }

    [Fact]
    public void ReadEphemeris_ObserverNotACelestialBody_Throws()
    {
        // The observer of the reads by identifiers is built as a celestial body: a spacecraft identifier ended the process
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(60.0));
        Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.ReadEphemeris(window, -82, 301, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(10.0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Occultation_EllipsoidOfASpacecraft_Throws(bool front)
    {
        // The frame of an ellipsoid read from the body information of a spacecraft identifier ended the process
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(3600.0));
        var e = Assert.Throws<ArgumentException>(() => front
            ? SpiceAPI.Instance.FindWindowsOnOccultationConstraint(window, 399, 10, ShapeType.Ellipsoid, -82, ShapeType.Ellipsoid,
                OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0))
            : SpiceAPI.Instance.FindWindowsOnOccultationConstraint(window, 399, -82, ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid,
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
                Vector3.Zero, TimeSystem.Time.J2000TDB.AddSeconds(i), i == eclipticIndex ? Frames.Frame.ECLIPTIC_J2000 : Frames.Frame.ICRF);
        }

        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteOrientation(file, -1560, records));
        Assert.Equal("stateOrientations", e.ParamName);
        file.Refresh();
        Assert.False(file.Exists);
    }
}
