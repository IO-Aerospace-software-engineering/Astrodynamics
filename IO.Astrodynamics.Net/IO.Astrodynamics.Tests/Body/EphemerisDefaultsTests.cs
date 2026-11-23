// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
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

using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The ephemeris reads of R20 (#668, lot L3): <see cref="ILocalizable.GetEphemeris(in TimeSystem.Time, ILocalizable, Frames.Frame, Aberration)"/>,
/// its window form and <see cref="SpiceAPI.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/> return the
/// <see cref="StateVector"/> they build, and <see cref="CelestialItem"/> and <see cref="Site"/> read in ICRF without
/// correction when no frame and no aberration are given, bit for bit the explicit read, on every kind of target: a body, a
/// propagated spacecraft, a spacecraft of the kernels, a site of the kernels or of coordinates, and a star.
/// </summary>
public class EphemerisDefaultsTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 0, 0, 0, TimeScale.TDB);

    public EphemerisDefaultsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static void AssertBitForBit(StateVector expected, StateVector actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Position.X), BitConverter.DoubleToInt64Bits(actual.Position.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Position.Y), BitConverter.DoubleToInt64Bits(actual.Position.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Position.Z), BitConverter.DoubleToInt64Bits(actual.Position.Z));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Velocity.X), BitConverter.DoubleToInt64Bits(actual.Velocity.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Velocity.Y), BitConverter.DoubleToInt64Bits(actual.Velocity.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Velocity.Z), BitConverter.DoubleToInt64Bits(actual.Velocity.Z));
        Assert.True(expected.Epoch.EqualsExact(actual.Epoch));
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Observer.NaifId, actual.Observer.NaifId);
    }

    // The default reads of a CelestialItem against its explicit reads, at one epoch and over a window
    private static void AssertDefaultsOfItem(CelestialItem target, ILocalizable observer, TimeSystem.Time epoch)
    {
        StateVector explicitRead = target.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None);
        StateVector defaultRead = target.GetEphemeris(epoch, observer);
        AssertBitForBit(explicitRead, defaultRead);

        var window = new Window(epoch, epoch.AddSeconds(120.0));
        IEnumerable<StateVector> explicitStates = target.GetEphemeris(window, observer, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(60.0));
        IEnumerable<StateVector> defaultStates = target.GetEphemeris(window, observer, TimeSpan.FromSeconds(60.0));
        var expected = explicitStates.ToArray();
        var actual = defaultStates.ToArray();
        Assert.Equal(3, expected.Length);
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertBitForBit(expected[i], actual[i]);
        }
    }

    [Fact]
    public void Body_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        AssertDefaultsOfItem(moon, earth, Epoch);
    }

    [Fact]
    public void Body_SeenFromABarycenter_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        AssertDefaultsOfItem(earth, new Barycenter(0), Epoch);
    }

    [Fact]
    public void PropagatedSpacecraft_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = new Spacecraft(-2001, "R20Defaults", 100.0, 1000.0, new Clock("R20DefaultsClk", 65536),
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.0, 1000.0), earth, Epoch, Frames.Frame.ICRF));
        spacecraft.Propagate(new Window(Epoch, Epoch.AddSeconds(600.0)), new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }, OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = [earth] });

        // From its central body (the short cut on the stored states) and from the Moon (the reference-body path)
        AssertDefaultsOfItem(spacecraft, earth, Epoch.AddSeconds(100.0));
        AssertDefaultsOfItem(spacecraft, new CelestialBody(PlanetsAndMoons.MOON), Epoch.AddSeconds(100.0));
    }

    [Fact]
    public void KernelSpacecraft_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var directory = Constants.OutputPath.CreateSubdirectory("EphemerisDefaults");
        var spk = new FileInfo(Path.Combine(directory.FullName, $"r20_{Guid.NewGuid():N}.spk"));
        var states = Enumerable.Range(0, 401).Select(i =>
        {
            double angle = i * 1e-3;
            return new StateVector(new Vector3(6800000.0 * System.Math.Cos(angle), 6800000.0 * System.Math.Sin(angle), 0.0),
                new Vector3(-7656.0 * System.Math.Sin(angle), 7656.0 * System.Math.Cos(angle), 0.0), earth, Epoch.AddSeconds(i), Frames.Frame.ICRF);
        }).ToArray();
        SpiceAPI.Instance.WriteEphemeris(spk, -9920, states);
        try
        {
            SpiceAPI.Instance.LoadKernels(spk);
            var spacecraft = new Spacecraft(-9920, "R20KernelDefaults", epoch: Epoch);
            AssertDefaultsOfItem(spacecraft, earth, Epoch.AddSeconds(100.0));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
        }
    }

    [Fact]
    public void Star_DefaultsAreIcrfGeometric()
    {
        var star = new Star(4472832, "R20Star", 2.9e29, "M4V", 9.5, 0.5469759,
            Equatorial.FromRightAscensionDeclination(4.7028, 0.0819, Epoch), 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, Epoch);
        AssertDefaultsOfItem(star, new CelestialBody(PlanetsAndMoons.EARTH), Epoch);
    }

    // The default reads of a site against its explicit reads
    private static void AssertDefaultsOfSite(Site site, ILocalizable observer)
    {
        StateVector explicitRead = site.GetEphemeris(Epoch, observer, Frames.Frame.ICRF, Aberration.None);
        AssertBitForBit(explicitRead, site.GetEphemeris(Epoch, observer));

        var window = new Window(Epoch, Epoch.AddSeconds(120.0));
        var expected = site.GetEphemeris(window, observer, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        var actual = site.GetEphemeris(window, observer, TimeSpan.FromSeconds(60.0)).ToArray();
        Assert.Equal(3, expected.Length);
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertBitForBit(expected[i], actual[i]);
        }
    }

    [Fact]
    public void KernelSite_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        AssertDefaultsOfSite(new Site(13, "DSS-13", earth), earth);
        AssertDefaultsOfSite(new Site(13, "DSS-13", earth), new CelestialBody(PlanetsAndMoons.MOON));
    }

    [Fact]
    public void CoordinateSite_DefaultsAreIcrfGeometric()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var site = new Site(339, "R20Site", earth, new Planetodetic(30.0 * Astrodynamics.Constants.Deg2Rad, 10.0 * Astrodynamics.Constants.Deg2Rad, 1000.0));
        AssertDefaultsOfSite(site, earth);
        AssertDefaultsOfSite(site, new CelestialBody(PlanetsAndMoons.MOON));
    }

    [Fact]
    public void ReadEphemeris_ReturnsTheStateVectorOfSpice()
    {
        // The state of the Moon from the Earth, typed as the StateVector it is, at an epoch and over a window
        StateVector state = SpiceAPI.Instance.ReadEphemeris(Epoch, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId, Frames.Frame.ICRF, Aberration.None);
        IEnumerable<StateVector> states = SpiceAPI.Instance.ReadEphemeris(new Window(Epoch, Epoch.AddSeconds(60.0)), PlanetsAndMoons.EARTH.NaifId,
            PlanetsAndMoons.MOON.NaifId, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(60.0));
        AssertBitForBit(state, states.First());
        AssertBitForBit(state, new CelestialBody(PlanetsAndMoons.MOON).GetEphemeris(Epoch, new CelestialBody(PlanetsAndMoons.EARTH)));
    }

    [Fact]
    public void ThroughTheInterface_TheReadIsAStateVector()
    {
        ILocalizable moon = new CelestialBody(PlanetsAndMoons.MOON);
        ILocalizable earth = new CelestialBody(PlanetsAndMoons.EARTH);
        StateVector state = moon.GetEphemeris(Epoch, earth, Frames.Frame.ICRF, Aberration.None);
        IEnumerable<StateVector> states = moon.GetEphemeris(new Window(Epoch, Epoch.AddSeconds(60.0)), earth, Frames.Frame.ICRF, Aberration.None,
            TimeSpan.FromSeconds(60.0));
        AssertBitForBit(state, states.First());
    }
}
