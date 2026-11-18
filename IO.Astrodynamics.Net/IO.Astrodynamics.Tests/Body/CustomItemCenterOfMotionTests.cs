// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// #747: the centre and the barycentre of motion of an item built from custom parameters (a <see cref="Spacecraft"/>, a
/// custom <see cref="CelestialBody"/>) are the observer of its initial orbital parameters and that observer's barycentre
/// (the observer itself when it is a barycentre), as the natural-body constructors give them; 0 and 0 without an initial
/// orbit, set again when <see cref="Spacecraft.SetInitialOrbitalParameters"/> replaces the orbit. Before the fix the
/// constructor of an item from custom parameters never set them: both were 0, the solar system barycentre, whatever the
/// orbit (measured on f70ee32: a spacecraft about the Earth, 0 and 0).
/// </summary>
/// <remarks>
/// The reference is SPICE's: the barycentre of a natural observer is the <c>BarycenterOfMotionId</c> of the record that
/// <see cref="SpiceAPI.GetCelestialBodyInfo"/> reads from the kernels (<c>GetCelestialBodyInfoProxy</c>: the Earth and the
/// Moon 3, the Sun 0), a barycentre its own identifier (NAIF Integer ID codes, the barycentres 0 to 9).
/// </remarks>
public class CustomItemCenterOfMotionTests
{
    public CustomItemCenterOfMotionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = new(2024, 3, 1, 12, 0, 0, frame: TimeSystem.TimeFrame.TDBFrame);

    // A body from custom parameters on the orbit given
    private sealed class CustomBody(int naifId, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
        : CelestialBody(naifId, "IOA_747_" + naifId, 1.0e20, 1000.0, 1000.0, orbit);

    private static StateVector OrbitAbout(ILocalizable observer, double radius = 7.0e6) =>
        new(new Vector3(radius, 0.0, 0.0), new Vector3(0.0, 1000.0, 0.0), observer, Epoch, Frames.Frame.ICRF);

    /// <summary>The barycentre of a natural observer as SPICE gives it, a barycentre itself.</summary>
    private static int SpiceBarycentre(int observerId) =>
        observerId < 10 ? observerId : SpiceAPI.Instance.GetCelestialBodyInfo(observerId).BarycenterOfMotionId;

    public static TheoryData<int> Observers => [399, 301, 10, 3, 0, 4];

    private static CelestialItem Observer(int naifId) => naifId < 10 ? new Barycenter(naifId) : new CelestialBody(naifId);

    [Theory]
    [MemberData(nameof(Observers))]
    public void Spacecraft_IsAboutTheObserverOfItsOrbit(int observerId)
    {
        // Arrange
        var observer = Observer(observerId);

        // Act
        var spacecraft = new Spacecraft(-747, "S747", 100.0, 1000.0, new Clock("S747", 65536), OrbitAbout(observer, 1.0e9));

        // Assert
        Assert.Equal(observerId, spacecraft.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(observerId), spacecraft.BarycenterOfMotionId);
    }

    [Theory]
    [MemberData(nameof(Observers))]
    public void CustomBody_IsAboutTheObserverOfItsOrbit(int observerId)
    {
        // Arrange
        var observer = Observer(observerId);

        // Act
        var body = new CustomBody(2747001, OrbitAbout(observer, 1.0e9));

        // Assert
        Assert.Equal(observerId, body.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(observerId), body.BarycenterOfMotionId);
    }

    [Fact]
    public void Spacecraft_OfTheStaticEarth_IsAboutTheEarth()
    {
        // Act: the deferred static object of PlanetsAndMoons as observer
        var spacecraft = new Spacecraft(-747, "S747", 100.0, 1000.0, new Clock("S747", 65536), OrbitAbout(PlanetsAndMoons.EARTH_BODY));

        // Assert
        Assert.Equal(399, spacecraft.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(399), spacecraft.BarycenterOfMotionId);
    }

    [Fact]
    public void Spacecraft_AboutASpacecraft_TakesItsBarycentre()
    {
        // Arrange: a spacecraft about the Moon, and a second about the first
        var mother = new Spacecraft(-748, "M747", 100.0, 1000.0, new Clock("M747", 65536), OrbitAbout(new CelestialBody(301)));

        // Act
        var daughter = new Spacecraft(-749, "D747", 100.0, 1000.0, new Clock("D747", 65536), OrbitAbout(mother, 100.0));

        // Assert: the centre is the observer, the barycentre that of the observer
        Assert.Equal(-748, daughter.CenterOfMotionId);
        Assert.Equal(3, daughter.BarycenterOfMotionId);
    }

    [Fact]
    public void KernelSpacecraft_IsAboutItsCentreOfMotion()
    {
        // Arrange: LRO (-85, Data/LRO), read about the Earth by default and about the Moon when given
        TestKernels.EnsureLro();
        var epoch = TimeSystem.Time.CreateTDB(757339200.0);

        // Act
        var aboutEarth = new Spacecraft(-85, "LRO", epoch: epoch);
        var aboutMoon = new Spacecraft(-85, "LRO", epoch: epoch, centerOfMotion: new CelestialBody(301));

        // Assert
        Assert.Equal(399, aboutEarth.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(399), aboutEarth.BarycenterOfMotionId);
        Assert.Equal(301, aboutMoon.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(301), aboutMoon.BarycenterOfMotionId);
    }

    [Theory]
    [InlineData(399, 301)]
    [InlineData(301, 399)]
    [InlineData(399, 499)]
    [InlineData(399, 3)]
    [InlineData(301, 10)]
    public void Spacecraft_GivenANewOrbit_IsAboutItsObserver(int firstObserverId, int newObserverId)
    {
        // Arrange: a spacecraft about the first observer
        var spacecraft = new Spacecraft(-747, "S747", 100.0, 1000.0, new Clock("S747", 65536), OrbitAbout(Observer(firstObserverId), 1.0e9));

        // Act: its initial orbit moved to another centre
        spacecraft.SetInitialOrbitalParameters(OrbitAbout(Observer(newObserverId), 1.0e9));

        // Assert: the centre and the barycentre follow the new observer
        Assert.Equal(newObserverId, spacecraft.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(newObserverId), spacecraft.BarycenterOfMotionId);
    }

    [Fact]
    public void Spacecraft_WithoutOrbit_GivenOne_IsAboutItsObserver()
    {
        // Arrange: a spacecraft built without an orbit, about nothing
        var spacecraft = new Spacecraft(-747, "S747", 100.0, 1000.0, new Clock("S747", 65536), null);
        Assert.Equal(0, spacecraft.CenterOfMotionId);
        Assert.Equal(0, spacecraft.BarycenterOfMotionId);

        // Act
        spacecraft.SetInitialOrbitalParameters(OrbitAbout(new CelestialBody(301)));

        // Assert
        Assert.Equal(301, spacecraft.CenterOfMotionId);
        Assert.Equal(SpiceBarycentre(301), spacecraft.BarycenterOfMotionId);
    }

    [Fact]
    public void CustomBody_WithoutOrbit_KeepsZero()
    {
        // Act
        var body = new CustomBody(2747002, null);

        // Assert: no initial orbit, no centre
        Assert.Equal(0, body.CenterOfMotionId);
        Assert.Equal(0, body.BarycenterOfMotionId);
    }
}
