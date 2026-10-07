using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class SolarRadiationPressureTests
{
    private readonly ITestOutputHelper _output;

    public SolarRadiationPressureTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeAcceleration()
    {
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);
        Assert.Equal(new Vector3(-8.456677063948622E-09, 4.237795744348693E-08, 1.8372880484798083E-08), res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ComputeAccelerationWithCr()
    {
        // Cr = 1.8 should scale the SRP by 1.8x compared to Cr = 1.0
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            solarRadiationCoeff: 1.8);
        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);

        // Expected = base result * 1.8
        var expected = new Vector3(-8.456677063948622E-09 * 1.8, 4.237795744348693E-08 * 1.8, 1.8372880484798083E-08 * 1.8);
        Assert.Equal(expected, res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ComputeAccelerationWithDynamicMass()
    {
        // Spacecraft with fuel tank — GetTotalMass() = dry mass + fuel = 100 + 50 = 150 kg
        // areaMassRatio = 1.0 / 150 instead of 1.0 / 100
        // So result should be 100/150 = 2/3 of the base result
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        var fuelTank = new FuelTank("tank1", "model1", "sn001", 100.0, 50.0);
        spc.AddFuelTank(fuelTank);

        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);

        // Expected = base result * (100/150)
        double ratio = 100.0 / 150.0;
        var expected = new Vector3(-8.456677063948622E-09 * ratio, 4.237795744348693E-08 * ratio, 1.8372880484798083E-08 * ratio);
        Assert.Equal(expected, res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ConstructorThrowsOnNullSpacecraft()
    {
        var earth = new CelestialBody(399);
        Assert.Throws<ArgumentNullException>(() => new SolarRadiationPressure(null, [earth]));
    }

    [Fact]
    public void ConstructorThrowsOnNullOccultingBodies()
    {
        var earth = new CelestialBody(399);
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        Assert.Throws<ArgumentNullException>(() => new SolarRadiationPressure(spc, null));
    }

    private static readonly TimeSystem.Time SunlitEpoch = new(2021, 3, 20, 12, 0, 0);

    private static (CelestialBody Earth, Spacecraft Spacecraft, StateVector State) SunlitLeo()
    {
        // Near the March equinox the Sun is close to +X of ICRF: a LEO state on +X is in sunlight.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, SunlitEpoch);
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth,
            SunlitEpoch, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, new Clock("My clock", 256), state, 10.0,
            2.2, null, 1.5);
        return (earth, spacecraft, state);
    }

    [Fact]
    public void Apply_SunAmongOccultingBodies_IsIgnored()
    {
        // Arrange (#356: the Sun used to eclipse itself, and the acceleration was exactly zero)
        var (earth, spacecraft, state) = SunlitLeo();
        var withoutSun = new SolarRadiationPressure(spacecraft, [earth]);
        var withSun = new SolarRadiationPressure(spacecraft, [earth, new CelestialBody(Stars.Sun)]);

        // Act
        var expected = withoutSun.Apply(state);
        var actual = withSun.Apply(state);

        // Assert
        Assert.True(expected.Magnitude() > 6e-7, $"SRP {expected.Magnitude():E3} m/s² in sunlight");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Apply_SunAsOnlyOccultingBody_GivesTheUnshadowedAcceleration()
    {
        // Arrange
        var (_, spacecraft, state) = SunlitLeo();
        var unshadowed = new SolarRadiationPressure(spacecraft, Array.Empty<CelestialBody>());
        var sunOnly = new SolarRadiationPressure(spacecraft, [new CelestialBody(Stars.Sun)]);

        // Act & Assert
        Assert.NotEqual(Vector3.Zero, unshadowed.Apply(state));
        Assert.Equal(unshadowed.Apply(state), sunOnly.Apply(state));
    }

    [Fact]
    public void Propagator_SunInTheBodies_AppliesSolarRadiationPressure()
    {
        // Arrange: the force the propagator builds from Earth, Moon and Sun, and the same force built without the Sun
        var (earth, spacecraft, state) = SunlitLeo();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, SunlitEpoch);
        var sun = new CelestialBody(Stars.Sun);
        var integrator = new RK78Integrator(1e-10, 1e-10);
        _ = new CentralBodyPropagator(new Window(SunlitEpoch, SunlitEpoch.AddHours(1.0)), spacecraft, integrator,
            new CelestialItem[] { earth, moon, sun }, false, true, TimeSpan.FromSeconds(60.0));
        var built = integrator.Forces.OfType<SolarRadiationPressure>().Single();
        var expected = new SolarRadiationPressure(spacecraft, [earth, moon]);

        // Act
        var actual = built.Apply(state);

        // Assert: the propagator's force reads the Sun and the Moon from its ephemeris cache (Lagrange interpolation),
        // the direct one from SPICE, hence a tolerance instead of bit equality
        var reference = expected.Apply(state);
        Assert.True(actual.Magnitude() > 6e-7, $"SRP {actual.Magnitude():E3} m/s² in sunlight");
        Assert.True((actual - reference).Magnitude() < 1e-12 * reference.Magnitude(),
            $"propagator SRP {actual}, direct SRP {reference}");
    }

    [Fact]
    public void Propagation_WithSolarRadiationPressure_DiffersFromWithout()
    {
        // Arrange: 3 h of LEO with Earth, Moon and Sun, A/m = 0.1 m²/kg, Cr = 1.5, so |a| ≈ 6.9e-7 m/s² in sunlight
        static Vector3 FinalPosition(bool includeSolarRadiationPressure)
        {
            var (earth, spacecraft, _) = SunlitLeo();
            var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, SunlitEpoch);
            var sun = new CelestialBody(Stars.Sun);
            var solution = spacecraft.Propagate(new Window(SunlitEpoch, SunlitEpoch.AddHours(3.0)),
                new CelestialItem[] { earth, moon, sun }, new RK78Integrator(1e-11, 1e-11), false,
                includeSolarRadiationPressure, TimeSpan.FromSeconds(60.0));
            return solution.StateVectors.Last().Position;
        }

        // Act
        double difference = (FinalPosition(true) - FinalPosition(false)).Magnitude();
        _output.WriteLine($"Final position difference with and without SRP: {difference:F3} m");

        // Assert: a t² / 2 is about 40 m over 3 h; eclipses and orbital dynamics change it by a factor of a few, not
        // by orders of magnitude. Before the fix of #356 the two propagations were identical.
        Assert.InRange(difference, 1.0, 1000.0);
    }
}
