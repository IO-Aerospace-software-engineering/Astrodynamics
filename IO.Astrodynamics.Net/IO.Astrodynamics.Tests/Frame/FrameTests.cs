using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
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
        Assert.Equal("J2000",frame.Name);
        Assert.Throws<ArgumentException>(()=>new Frames.Frame(""));
    }

    [Fact]
    public void ToInertialFrame()
    {
        var so = Frames.Frame.ICRF.ToFrame(Frames.Frame.ECLIPTIC_J2000, TimeSystem.Time.J2000TDB);
        Assert.Equal(so,
            new StateOrientation(new Quaternion(0.9791532214288993, new Vector3(-0.20312303898231013, 0.0, 0.0)), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
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
        Assert.Equal(new Quaternion(0.5044792585297516, 0.20093165566257334, 0.06427003630843892, 0.8372553433086475), q.Rotation);
        Assert.Equal(new Vector3(1.9805391781278776E-05, 2.263201244975088E-05, 6.376864584934005E-05), q.AngularVelocity);
        Assert.Equal(7.0504622008731998E-05, q.AngularVelocity.Magnitude());
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