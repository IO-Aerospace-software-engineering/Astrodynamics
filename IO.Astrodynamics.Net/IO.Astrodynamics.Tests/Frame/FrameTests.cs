using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using IO.Astrodynamics.SolarSystemObjects;
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
        Assert.Equal("J2000",frame.Name);
        Assert.Throws<ArgumentException>(()=>new Frames.Frame(""));
    }

    [Fact]
    public void ToInertialFrame()
    {
        var so = Frames.Frame.ICRF.ToFrame(Frames.Frame.ECLIPTIC_J2000, DateTimeExtension.J2000);
        Assert.Equal(so,
            new StateOrientation(new Quaternion(0.9791532214288993, new Vector3(-0.20312303898231013, 0.0, 0.0)), Vector3.Zero, DateTimeExtension.J2000, Frames.Frame.ICRF));
    }

    [Fact]
    public void ToNonInertialFrame()
    {
        var epoch = DateTimeExtension.J2000;
        var moonFrame = new Frames.Frame(PlanetsAndMoons.MOON.Frame);
        var earthFrame = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var q = moonFrame.ToFrame(earthFrame, epoch);

        Assert.Equal(epoch, q.Epoch);
        Assert.Equal(moonFrame, q.ReferenceFrame);
        Assert.Equal(new Quaternion(0.5044792582956342, 0.2009316556383325, 0.06427003637545137, 0.8372553434503859), q.Rotation);
        Assert.Equal(new Vector3(1.980539178135755E-05, 2.2632012450014214E-05, 6.376864584829888E-05), q.AngularVelocity);
        Assert.Equal(7.050462200789696E-05, q.AngularVelocity.Magnitude());
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