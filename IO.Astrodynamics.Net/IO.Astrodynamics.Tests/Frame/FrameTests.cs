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
        TestKernels.EnsureSolarSystem();
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

    // #562: the frames of the library and the frames built by name a caller can give (a CCSDS file, the CLI, user code),
    // as SPICE reads them: the name in any case (namfrm_c ignores it)
    public static TheoryData<string, string> SameFramePairs => new()
    {
        { "GCRF", "GCRF" }, { "GCRF", "gcrf" }, { "CIRS", "CIRS" }, { "CIRS", "cirs" }, { "TIRS", "TIRS" }, { "TEME", "TEME" },
        { "TEME", "Teme" }, { "ICRF", "J2000" }, { "ICRF", "j2000" }, { "ECLIPTIC_J2000", "ECLIPJ2000" }
    };

    private static Frames.Frame Static(string name) => name switch
    {
        "GCRF" => Frames.Frame.GCRF,
        "CIRS" => Frames.Frame.CIRS,
        "TIRS" => Frames.Frame.TIRS,
        "TEME" => Frames.Frame.TEME,
        "ICRF" => Frames.Frame.ICRF,
        "ECLIPTIC_J2000" => Frames.Frame.ECLIPTIC_J2000,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [Theory]
    [MemberData(nameof(SameFramePairs))]
    public void AFrameBuiltByName_IsTheFrameOfTheLibrary_ForEveryRelation(string library, string name)
    {
        // #562: ==, !=, Equals(Frame), Equals(object), GetHashCode and the collections are one relation, by the name
        // ignoring case; before, == and Equals(object) also compared the type, so new Frame("GCRF") == Frame.GCRF was false
        // while Equals(Frame) and HashSet said true (the issue's table)
        var a = Static(library);
        var b = new Frames.Frame(name);
        Assert.True(a == b);
        Assert.True(b == a);
        Assert.False(a != b);
        Assert.False(b != a);
        Assert.True(a.Equals(b));
        Assert.True(b.Equals(a));
        Assert.True(a.Equals((object)b));
        Assert.True(b.Equals((object)a));
        Assert.True(Equals(a, b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Contains(b, new System.Collections.Generic.HashSet<Frames.Frame> { a });
        Assert.Contains(a, new System.Collections.Generic.HashSet<Frames.Frame> { b });
        Assert.Equal(1, new System.Collections.Generic.Dictionary<Frames.Frame, int> { [a] = 1 }[b]);
        Assert.Equal(a, b);
    }

    [Fact]
    public void FramesOfOtherNames_AreDifferent_ForEveryRelation()
    {
        // Every pair of distinct names among the frames of the library, and a frame built by another name: different for
        // every relation. ICRF is named J2000: "ICRF" is another name. Null is equal to null only
        Frames.Frame[] frames =
        [
            Frames.Frame.ICRF, Frames.Frame.GCRF, Frames.Frame.CIRS, Frames.Frame.TIRS, Frames.Frame.TEME, Frames.Frame.ECLIPTIC_J2000,
            Frames.Frame.ECLIPTIC_B1950, Frames.Frame.B1950, Frames.Frame.FK4, Frames.Frame.GALACTIC_SYSTEM2, new Frames.Frame("ITRF93"),
            new Frames.Frame("ICRF"), new Frames.Frame("TEME_OF_DATE")
        ];
        for (int i = 0; i < frames.Length; i++)
        {
            for (int j = 0; j < frames.Length; j++)
            {
                if (i == j) continue;
                Assert.False(frames[i] == frames[j], $"{frames[i]} == {frames[j]}");
                Assert.True(frames[i] != frames[j]);
                Assert.False(frames[i].Equals(frames[j]));
                Assert.False(frames[i].Equals((object)frames[j]));
            }

            Assert.False(frames[i] == null);
            Assert.False(null == frames[i]);
            Assert.True(frames[i] != null);
            Assert.False(frames[i].Equals((Frames.Frame)null));
        }

        Frames.Frame none = null;
        Assert.True(none == null);
        Assert.False(none != null);
    }

    [Fact]
    public void TheFramesOfASpacecraftAndOfASite_AreTheirNames()
    {
        // The subclasses that are not static frames: a SpacecraftFrame and a SiteFrame are equal to a frame built by their
        // name, in any case, for every relation (they were different for == and Equals(object))
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-1562, "EQ562", 1000.0, 3000.0, new IO.Astrodynamics.Body.Spacecraft.Clock("eq562", 65536),
            new StateVector(new Vector3(6778137.0, 0.0, 0.0), new Vector3(0.0, 7668.6, 0.0), TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        var site = new Site(339, "EQ562SITE", TestHelpers.EarthAtJ2000, new Planetodetic(-2.0384478466737517, 0.61517960506340708, 1073.2434632601216));
        foreach (var frame in new Frames.Frame[] { spacecraft.Frame, site.Frame })
        {
            foreach (var byName in new[] { new Frames.Frame(frame.Name), new Frames.Frame(frame.Name.ToLowerInvariant()) })
            {
                Assert.True(frame == byName);
                Assert.False(frame != byName);
                Assert.True(frame.Equals((object)byName));
                Assert.True(byName.Equals((object)frame));
                Assert.Equal(frame.GetHashCode(), byName.GetHashCode());
            }

            Assert.False(frame == Frames.Frame.ICRF);
        }
    }

    [Fact]
    public void AStateInAFrameBuiltByName_IsTheStateInTheFrameOfTheLibrary()
    {
        // The library's comparisons with == (#562): StateVector.Equals, and the identity shortcut of ToFrame. A state in
        // Frame.CIRS converted to new Frame("CIRS") is the same state (the identity, the closed form), where it went to SPICE
        // with the name CIRS and came back r = (0, 0, 0)
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var position = new Vector3(6778137.0, 1234.5, -4321.0);
        var velocity = new Vector3(-1.5, 7668.6, 2.25);
        foreach (var (library, name) in new[] { (Frames.Frame.GCRF, "GCRF"), (Frames.Frame.CIRS, "CIRS"), (Frames.Frame.TIRS, "tirs"), (Frames.Frame.TEME, "TEME") })
        {
            var byName = new Frames.Frame(name);
            var inLibrary = new StateVector(position, velocity, TestHelpers.EarthAtJ2000, epoch, library);
            var inByName = new StateVector(position, velocity, TestHelpers.EarthAtJ2000, epoch, byName);
            Assert.True(inLibrary.Equals(inByName));
            Assert.True(inLibrary == inByName);

            var converted = inLibrary.ToFrame(byName).ToStateVector();
            Assert.Equal(position, converted.Position);
            Assert.Equal(velocity, converted.Velocity);
            var back = inByName.ToFrame(library).ToStateVector();
            Assert.Equal(position, back.Position);
            Assert.Equal(velocity, back.Velocity);

            var transform = library.ToFrame(byName, epoch);
            Assert.Equal(Quaternion.Identity, transform.Rotation);
            Assert.Equal(Vector3.Zero, transform.AngularVelocity);
        }
    }

    [Fact]
    public void TheOrientationsAddedToAFrame_AreKeptByEpoch_TheFirstAtAnEpochKept()
    {
        // The members of Frame that the attitude record of a spacecraft overrides (#523, virtual since #537): on another
        // frame they keep the orientations added, in epoch order, the first one at an epoch, until they are cleared
        var frame = new Frames.Frame("FRAME_ORIENTATIONS_TEST");
        var t0 = TimeSystem.Time.J2000TDB;
        var t1 = t0.AddSeconds(10.0);
        var later = new StateOrientation(new Quaternion(Vector3.VectorZ, 0.1), Vector3.Zero, t1, frame);
        var earlier = new StateOrientation(new Quaternion(Vector3.VectorZ, 0.2), Vector3.Zero, t0, frame);
        var sameEpoch = new StateOrientation(new Quaternion(Vector3.VectorZ, 0.3), Vector3.Zero, t1, frame);

        Assert.True(frame.AddStateOrientationToICRF(later));
        Assert.True(frame.AddStateOrientationToICRF(earlier));
        Assert.False(frame.AddStateOrientationToICRF(sameEpoch));
        Assert.Equal(new[] { earlier, later }, frame.GetStateOrientationsToICRF());
        Assert.Equal(later, frame.GetLatestStateOrientationToICRF());

        frame.ClearStateOrientations();
        Assert.Empty(frame.GetStateOrientationsToICRF());
        Assert.Null(frame.GetLatestStateOrientationToICRF());
        Assert.Equal(new Frames.Frame("FRAME_ORIENTATIONS_TEST").GetHashCode(), frame.GetHashCode());
    }
}
