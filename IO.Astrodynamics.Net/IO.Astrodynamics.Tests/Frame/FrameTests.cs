using System;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class FrameTests
{
    public FrameTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        Frames.Frame frame = new Frames.Frame("J2000");
        Assert.Equal("J2000", frame.Name);
        Assert.Throws<ArgumentException>(() => new Frames.Frame(""));
    }

    [Fact]
    public void ToInertialFrame()
    {
        var so = Frames.Frame.ICRF.ToFrame(Frames.Frame.ECLIPTIC_J2000, TimeSystem.Time.J2000TDB);
        Assert.Equal(
            new StateOrientation(new Quaternion(0.9791532214288993, new Vector3(-0.20312303898231013, 0.0, 0.0)), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), so);
    }

    [Fact]
    public void ToNonInertialFrame()
    {
        var epoch = TimeSystem.Time.J2000TDB;
        var moonFrame = new Frames.Frame(PlanetsAndMoons.MOON.Frame);
        var earthFrame = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var q = moonFrame.ToFrame(earthFrame, epoch);

        Assert.Equal(epoch, q.Epoch);
        Assert.Equal(moonFrame, q.ReferenceFrame);
        Assert.Equal(new Quaternion(0.5044792585297516, 0.20093165566257334, 0.06427003630843892, 0.8372553433086475).VectorPart, q.Rotation.VectorPart,
            TestHelpers.VectorComparer);
        Assert.Equal(new Quaternion(0.5044792585297516, 0.20093165566257334, 0.06427003630843892, 0.8372553433086475).W, q.Rotation.W, 9);

        Assert.Equal(new Vector3(1.9805391781278783E-05, 2.2632012449750882E-05, 6.376864584934008E-05), q.AngularVelocity);
        Assert.Equal(7.0504622008732038E-05, q.AngularVelocity.Magnitude());
    }

    [Fact]
    public void SiteFrame()
    {
        var site = new Site(339, "TestSite", TestHelpers.EarthAtJ2000, new Planetodetic(-2.0384478466737517, 0.61517960506340708, 1073.2434632601216));
        var res = site.Frame.ToFrame(Frames.Frame.ICRF, Astrodynamics.TimeSystem.Time.J2000TDB);
        Assert.Equal(new Quaternion(0.8786982934817297, -0.06636828545847889, -0.4550266984882365, -0.12820009118754108), res.Rotation);
        Assert.Equal(new Vector3(5.9553215865189754E-05, 2.3226543847344109E-09, 4.2082165983713808E-05), res.AngularVelocity, TestHelpers.VectorComparer);
    }

    [Fact]
    public void FrameToString()
    {
        Assert.Equal("J2000", Frames.Frame.ICRF.ToString());
    }

    [Fact]
    public void Equality()
    {
        Assert.Equal(new Frames.Frame("J2000"), Frames.Frame.ICRF);
        Assert.True(new Frames.Frame("J2000") == Frames.Frame.ICRF);
        Assert.True(Frames.Frame.ECLIPTIC_J2000 != Frames.Frame.ICRF);
        Assert.True(Frames.Frame.ECLIPTIC_J2000.Equals(Frames.Frame.ECLIPTIC_J2000));
        Assert.True(Frames.Frame.ECLIPTIC_J2000.Equals((object)Frames.Frame.ECLIPTIC_J2000));
        Assert.False(Frames.Frame.ECLIPTIC_J2000.Equals(null));
        Assert.False(Frames.Frame.ECLIPTIC_J2000.Equals((object)null));
        Assert.False(Frames.Frame.ECLIPTIC_J2000.Equals("null"));
    }
}