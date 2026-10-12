// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

/// <summary>
/// SRP, albedo and thermal pressure read geometric positions, so the same physical state gives the same acceleration
/// relative to the Earth and relative to the Sun (#360).
/// </summary>
/// <remarks>
/// With the light time from the observer of the state, the Earth seen from the Sun was moved by its velocity times
/// about 499 s, some 15,000 km: on these states the acceleration relative to the Sun was 6.9e-7 m/s² in the umbra
/// instead of zero, and the albedo and thermal accelerations differed by 90 % to 96 %.
/// </remarks>
public class RadiationPressureObserverTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    // The heliocentric coordinates round the position by about 1e-5 m (1.5e11 m at a relative 1e-16), which moves the
    // acceleration by its gradient: about 1/r relative in sunlight, measured 3.8e-12 at most (the terminator albedo),
    // and 1 / (width of the penumbra, about 65 km at 3000 km behind the Earth) in the penumbra, measured 1.75e-10. The
    // tolerances are three to six times these.
    private const double ObserverTolerance = 1e-11;
    private const double PenumbraObserverTolerance = 1e-9;

    // The same formulas on the same doubles, in another order: measured 2.8e-16 at most.
    private const double ModelTolerance = 1e-15;

    // The 8-point Lagrange interpolation of the cache at 1234.5 s from the start of its 60 s grid: measured 1.7e-17.
    private const double CacheTolerance = 1e-15;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public RadiationPressureObserverTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, albedo: 0.3,
            thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
    }

    public static TheoryData<string> Geometries => new() { "umbra", "penumbra", "sunlit", "terminator" };

    [Theory]
    [MemberData(nameof(Geometries))]
    public void SameStateRelativeToTheEarthOrToTheSun_GivesTheSameAcceleration(string geometry)
    {
        // Arrange
        var relativeToEarth = EarthRelativeState(geometry);
        var relativeToSun = relativeToEarth.RelativeTo(Stars.SUN_BODY, Aberration.None).ToStateVector();
        var spacecraft = CreateSpacecraft(relativeToEarth);

        foreach (var (name, force) in Forces(spacecraft))
        {
            // Act
            var fromEarth = force.Apply(relativeToEarth);
            var fromSun = force.Apply(relativeToSun);

            // Assert
            double difference = (fromEarth - fromSun).Magnitude();
            double tolerance = geometry == "penumbra" ? PenumbraObserverTolerance : ObserverTolerance;
            _output.WriteLine($"{geometry} {name}: |a| {fromEarth.Magnitude():E4} m/s², difference {difference:E3}");
            Assert.True(difference <= tolerance * fromEarth.Magnitude(),
                $"{geometry} {name}: {difference:E3} m/s² between the two observers, |a| = {fromEarth.Magnitude():E3}");
        }
    }

    [Fact]
    public void UmbraRelativeToTheSun_IsZero()
    {
        // Arrange
        var relativeToSun = EarthRelativeState("umbra").RelativeTo(Stars.SUN_BODY, Aberration.None).ToStateVector();
        var force = new SolarRadiationPressure(CreateSpacecraft(relativeToSun), new[] { _earth });

        // Act
        var acceleration = force.Apply(relativeToSun);

        // Assert
        Assert.Equal(Vector3.Zero, acceleration);
    }

    [Theory]
    [MemberData(nameof(Geometries))]
    public void RelativeToTheSun_MatchesTheModelsOnTheGeometricPositionsOfSpice(string geometry)
    {
        // Arrange: the Earth from the Sun by SPICE, geometric, and the models written out
        var state = EarthRelativeState(geometry).RelativeTo(Stars.SUN_BODY, Aberration.None).ToStateVector();
        var spacecraft = CreateSpacecraft(state);
        var earthFromSun = _earth.GetEphemeris(Epoch, Stars.SUN_BODY, Frames.Frame.ICRF, Aberration.None)
            .ToStateVector().Position;
        double areaMassRatio = spacecraft.SectionalArea / spacecraft.GetTotalMass();
        double cr = spacecraft.SolarRadiationCoeff;
        double luminosityTerm = IO.Astrodynamics.Constants.SolarMeanRadiativeLuminosity /
                                (4.0 * System.Math.PI * IO.Astrodynamics.Constants.C);
        var r = state.Position;
        var earthToSc = r - earthFromSun;

        // Cannonball SRP, scaled by the shadow fraction of the Earth seen from the spacecraft
        double shadow = Stars.SUN_BODY.ShadowFraction(_earth, state, Aberration.None);
        Assert.Equal(geometry switch { "umbra" => 1.0, "penumbra" => shadow, _ => 0.0 }, shadow);
        Assert.True(geometry != "penumbra" || shadow is > 0.0 and < 1.0, $"penumbra: shadow fraction {shadow}");
        var expectedSrp = r / System.Math.Pow(r.Magnitude(), 3.0) * luminosityTerm * cr * areaMassRatio * (1.0 - shadow);

        // Lambertian albedo and isotropic thermal emission, radially from the Earth
        double phi = earthFromSun.Inverse().Angle(earthToSc);
        double lambertian = System.Math.Max((System.Math.PI - phi) * System.Math.Cos(phi) + System.Math.Sin(phi), 0.0);
        double radiusOverSun = _earth.EquatorialRadius / earthFromSun.Magnitude();
        var expectedAlbedo = earthToSc.Normalize() * (luminosityTerm * 0.3 * cr * areaMassRatio * 2.0 /
            (3.0 * System.Math.PI) * radiusOverSun * radiusOverSun * lambertian / earthToSc.MagnitudeSquared());
        double t2 = 254.0 * 254.0;
        var expectedThermal = earthToSc.Normalize() * (cr * areaMassRatio * 0.95 * IO.Astrodynamics.Constants.StefanBoltzmann *
            t2 * t2 * _earth.EquatorialRadius * _earth.EquatorialRadius / IO.Astrodynamics.Constants.C /
            earthToSc.MagnitudeSquared());

        var expected = new[] { expectedSrp, expectedAlbedo, expectedThermal };
        var forces = Forces(spacecraft);
        for (int i = 0; i < forces.Length; i++)
        {
            // Act
            var actual = forces[i].Force.Apply(state);

            // Assert
            double difference = (actual - expected[i]).Magnitude();
            _output.WriteLine($"{geometry} {forces[i].Name}: |a| {expected[i].Magnitude():E4} m/s², difference {difference:E3}");
            Assert.True(difference <= ModelTolerance * expected[i].Magnitude(),
                $"{geometry} {forces[i].Name}: {difference:E3} m/s² from the model, |a| = {expected[i].Magnitude():E3}");
        }
    }

    [Theory]
    [MemberData(nameof(Geometries))]
    public void WithTheEphemerisCache_MatchesSpice(string geometry)
    {
        // Arrange: the cache of the propagator, geometric positions relative to the Earth
        var state = EarthRelativeState(geometry);
        var spacecraft = CreateSpacecraft(state);
        var cache = new PropagationEphemerisCache(new TimeSystem.Window(Epoch.AddHours(-1.0), Epoch.AddHours(1.0)),
            new (CelestialItem, Aberration)[] { (_earth, Aberration.None), (Stars.SUN_BODY, Aberration.None) }, _earth,
            Frames.Frame.ICRF, TimeSpan.FromSeconds(60.0));
        var epoch = Epoch.AddSeconds(1234.5);
        var shifted = new StateVector(state.Position, state.Velocity, _earth, epoch, Frames.Frame.ICRF);

        foreach (var (name, force) in Forces(spacecraft))
        {
            // Act
            var fromSpice = force.Apply(shifted);
            force.EphemerisCache = cache;
            var fromCache = force.Apply(shifted);

            // Assert
            double difference = (fromCache - fromSpice).Magnitude();
            _output.WriteLine($"{geometry} {name}: |a| {fromSpice.Magnitude():E4} m/s², difference {difference:E3}");
            Assert.True(difference <= CacheTolerance * fromSpice.Magnitude(),
                $"{geometry} {name}: {difference:E3} m/s² between the cache and SPICE, |a| = {fromSpice.Magnitude():E3}");
        }
    }

    private (string Name, ForceBase Force)[] Forces(Spacecraft spacecraft)
    {
        return new (string, ForceBase)[]
        {
            ("SRP", new SolarRadiationPressure(spacecraft, new[] { _earth })),
            ("albedo", new AlbedoRadiationPressure(spacecraft, _earth)),
            ("thermal", new ThermalRadiationPressure(spacecraft, _earth))
        };
    }

    // A state 7000 km from the Earth's centre, at a velocity of 7546 m/s, in the given geometry with respect to the Sun
    private StateVector EarthRelativeState(string geometry)
    {
        var sunDirection = Stars.SUN_BODY.GetEphemeris(Epoch, _earth, Frames.Frame.ICRF, Aberration.None).ToStateVector()
            .Position.Normalize();
        var side = sunDirection.Cross(Vector3.VectorZ).Normalize();
        var (position, velocity) = geometry switch
        {
            "umbra" => (sunDirection * -7000e3, side * 7546.0),
            // On the edge of the shadow cylinder, 3000 km behind the Earth: partly eclipsed
            "penumbra" => (sunDirection * -3000e3 + side * 6378.137e3, sunDirection * 7546.0),
            "sunlit" => (sunDirection * 7000e3, side * 7546.0),
            "terminator" => (side * 7000e3, sunDirection * 7546.0),
            _ => throw new ArgumentOutOfRangeException(nameof(geometry))
        };
        return new StateVector(position, velocity, _earth, Epoch, Frames.Frame.ICRF);
    }

    private static Spacecraft CreateSpacecraft(StateVector state)
    {
        return new Spacecraft(-1360, "OBSERVER360", 100.0, 1000.0, new Clock("observer360", 65536), state, 10.0, 2.2,
            null, 1.5);
    }
}
