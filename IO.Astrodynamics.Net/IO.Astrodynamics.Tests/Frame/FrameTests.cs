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
    // Reference for the angular velocity of the DSS-13 site frame: the SPICE frame DSS-13_TOPO (earth_topo_201023.tf).
    // Its axes differ by 5.5e-7 rad from the frame built from the site coordinates, which moves the angular velocity
    // by 4e-11 rad/s, well within the 1e-9 rad/s of VelocityVectorComparer.
    private static Frames.Frame SpiceDss13TopocentricFrame => new("DSS-13_TOPO");

    public FrameTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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

        Assert.Equal(new Vector3(1.9805391781278783E-05, 2.2632012449750882E-05, 6.376864584934008E-05), q.AngularVelocity,TestHelpers.VectorComparer);
        Assert.Equal(7.0504622008732038E-05, q.AngularVelocity.Magnitude(),6);
    }

    [Fact]
    public void SiteFrame()
    {
        var site = new Site(339, "TestSite", TestHelpers.EarthAtJ2000, new Planetodetic(-2.0384478466737517, 0.61517960506340708, 1073.2434632601216));
        var res = site.Frame.ToFrame(Frames.Frame.ICRF, Astrodynamics.TimeSystem.Time.J2000TDB);
        Assert.Equal(new Quaternion(0.8786982934817295, -0.06636828545847888, -0.4550266984882364, -0.12820009118754105), res.Rotation, TestHelpers.QuaternionComparer);
        Assert.Equal(SpiceDss13TopocentricFrame.ToFrame(Frames.Frame.ICRF, Astrodynamics.TimeSystem.Time.J2000TDB).AngularVelocity, res.AngularVelocity,
            TestHelpers.VelocityVectorComparer);
    }
    
    [Fact]
    public void SiteFrameRelativeToMoon()
    {
        var site = new Site(339, "TestSite", TestHelpers.EarthAtJ2000, new Planetodetic(-2.0384478466737517, 0.61517960506340708, 1073.2434632601216));
        var res = site.Frame.ToFrame(TestHelpers.MoonAtJ2000.Frame, Astrodynamics.TimeSystem.Time.J2000TDB);
        Assert.Equal(new Quaternion(0.7944038930635089, -0.3881980140265745, -0.3544407946772864, -0.30429669676139814), res.Rotation, TestHelpers.QuaternionComparer);
        Assert.Equal(SpiceDss13TopocentricFrame.ToFrame(TestHelpers.MoonAtJ2000.Frame, Astrodynamics.TimeSystem.Time.J2000TDB).AngularVelocity,
            res.AngularVelocity, TestHelpers.VelocityVectorComparer);
    }
    
    [Fact]
    public void SiteFrameRelativeToMoonFromSpice()
    {
        Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
        var res = site.Frame.ToFrame(TestHelpers.MoonAtJ2000.Frame, Astrodynamics.TimeSystem.Time.J2000TDB);
        Assert.Equal(new Quaternion(0.7944038930635089, -0.3881980140265745, -0.3544407946772864, -0.30429669676139814), res.Rotation, TestHelpers.QuaternionComparer);
        Assert.Equal(SpiceDss13TopocentricFrame.ToFrame(TestHelpers.MoonAtJ2000.Frame, Astrodynamics.TimeSystem.Time.J2000TDB).AngularVelocity,
            res.AngularVelocity, TestHelpers.VelocityVectorComparer);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void SiteFrameMatchesSpiceTopocentricFrame(int year)
    {
        // DSS-13_TOPO (earth_topo_201023.tf) is the SPICE topocentric frame of DSS-13, x north, y west, z zenith,
        // fixed in ITRF93 like the site frame of the library. The site is placed at the planetodetic coordinates of the
        // frame kernel, so that both frames have the same axes.
        var epoch = new TimeSystem.Time(year, 1, 1, 12, 0, 0);
        var site = CreateSiteAtSpiceDss13Coordinates();

        var actual = site.Frame.GetStateOrientationToICRF(epoch);
        var expected = new Frames.Frame("DSS-13_TOPO").GetStateOrientationToICRF(epoch);

        var relative = expected.Rotation.Conjugate() * actual.Rotation;
        double angle = 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
        Assert.True(angle < 1e-12, $"Rotation difference {angle:E3} rad");
        double difference = (actual.AngularVelocity - expected.AngularVelocity).Magnitude();
        Assert.True(difference < 1e-16, $"Angular velocity difference {difference:E3} rad/s");
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void StateInSiteFrameMatchesSpiceTopocentricFrame(int year)
    {
        var epoch = new TimeSystem.Time(year, 1, 1, 12, 0, 0);
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = PlanetsAndMoons.MOON_BODY;
        var site = CreateSiteAtSpiceDss13Coordinates();
        var inIcrf = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var expected = moon.GetEphemeris(epoch, earth, new Frames.Frame("DSS-13_TOPO"), Aberration.None).ToStateVector();

        var actual = inIcrf.ToFrame(site.Frame).ToStateVector();

        double positionError = (actual.Position - expected.Position).Magnitude();
        double velocityError = (actual.Velocity - expected.Velocity).Magnitude();
        Assert.True(positionError < 1e-12 * expected.Position.Magnitude(), $"Position error {positionError:E3} m");
        Assert.True(velocityError < 1e-12 * expected.Velocity.Magnitude(), $"Velocity error {velocityError:E3} m/s");
    }

    [Fact]
    public void SiteFrameDoesNotRotateRelativeToItrf93()
    {
        // The former site frame angular velocity was expressed in the wrong axes: 1.5e-7 rad/s relative to ITRF93 in
        // 2021, 44 m/s on the Moon seen in the site frame.
        var epoch = new TimeSystem.Time(2021, 1, 1, 12, 0, 0);
        var site = new Site(13, "DSS-13", PlanetsAndMoons.EARTH_BODY);

        var orientation = new Frames.Frame("ITRF93").ToFrame(site.Frame, epoch);

        Assert.True(orientation.AngularVelocity.Magnitude() < 1e-18, $"|w| = {orientation.AngularVelocity.Magnitude():E3} rad/s");
    }

    private static Site CreateSiteAtSpiceDss13Coordinates()
    {
        // Planetodetic coordinates of DSS-13 in earth_topo_201023.tf.
        return new Site(113, "SPICE-DSS-13", PlanetsAndMoons.EARTH_BODY,
            new Planetodetic(-116.7944627147624 * IO.Astrodynamics.Constants.Deg2Rad, 35.2471635434595 * IO.Astrodynamics.Constants.Deg2Rad,
                1070.439519876));
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