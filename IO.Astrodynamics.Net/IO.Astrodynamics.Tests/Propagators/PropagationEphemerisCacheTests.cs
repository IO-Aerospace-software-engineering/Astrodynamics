// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

public class PropagationEphemerisCacheTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    public PropagationEphemerisCacheTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static (CelestialBody Earth, CelestialBody Moon, Window Window, (CelestialItem, Aberration)[] Entries) Setup()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        var window = new Window(Epoch.ToTDB().AddHours(-1.0), Epoch.ToTDB().AddHours(1.0));
        return (earth, moon, window, new (CelestialItem, Aberration)[] { (moon, Aberration.None) });
    }

    [Fact]
    public void Constructor_WithoutFrame_CachesInIcrf()
    {
        // Arrange
        var (earth, _, window, entries) = Setup();

        // Act
        var cache = new PropagationEphemerisCache(window, entries, earth, TimeSpan.FromSeconds(60));

        // Assert
        Assert.Equal(Frames.Frame.ICRF, cache.Frame);
    }

    [Fact]
    public void Constructor_WithFrame_CachesTheVectorsInThatFrame()
    {
        // Arrange
        var (earth, _, window, entries) = Setup();
        var icrf = new PropagationEphemerisCache(window, entries, earth, TimeSpan.FromSeconds(60));
        var epoch = Epoch.ToTDB().AddSeconds(1234.5);

        // Act
        var ecliptic = new PropagationEphemerisCache(window, entries, earth, Frames.Frame.ECLIPTIC_J2000,
            TimeSpan.FromSeconds(60));

        // Assert: the ecliptic position is the ICRF position rotated, off the grid of the cache
        var icrfPosition = icrf.GetPosition(PlanetsAndMoons.MOON.NaifId, Aberration.None, epoch);
        var expected = new StateVector(icrfPosition, Vector3.Zero, earth, epoch, Frames.Frame.ICRF)
            .ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector().Position;
        var actual = ecliptic.GetPosition(PlanetsAndMoons.MOON.NaifId, Aberration.None, epoch);
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, ecliptic.Frame);
        Assert.True((actual - expected).Magnitude() < 1e-12 * expected.Magnitude(), $"{actual} instead of {expected}");
    }

    [Fact]
    public void Constructor_NullFrame_Throws()
    {
        // Arrange
        var (earth, _, window, entries) = Setup();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PropagationEphemerisCache(window, entries, earth, null, TimeSpan.FromSeconds(60)));
    }
}
